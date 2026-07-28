type Coordinate = { q: number; r: number };
type Unit = { id: string; ownerPlayerId: string; position: Coordinate };
type Player = { id: string; displayName: string; kind: string };
type KingOfTheHillState = {
  gameDefinitionId: string;
  board: { radius: number };
  players: Player[];
  units: Unit[];
  controlScores: Record<string, number>;
  currentPlayerId: string;
  turnNumber: number;
  isCompleted: boolean;
  winnerPlayerId: string | null;
  objectiveCoordinate: Coordinate;
  currentPlayer: Player;
};
type MatchResponse = {
  matchId: string;
  gameDefinitionId: string;
  lastMessage: string;
  state: KingOfTheHillState;
};
type SelectedUnit = Unit | null;

declare global {
  interface Window {
    kingOfTheHillConfig?: {
      backendBaseUrl?: string;
      gameDefinitionId?: string;
    };
  }
}

class KingOfTheHillClient {
  constructor(
    private readonly backendBaseUrl: string,
    private readonly gameDefinitionId: string
  ) {}

  async createMatch(): Promise<MatchResponse> {
    const response = await fetch(
      `${this.backendBaseUrl}/api/games/${this.gameDefinitionId}/matches`,
      { method: "POST" }
    );

    return this.readJson(response);
  }

  async sendCommand(
    matchId: string,
    commandName: string,
    args?: Record<string, string>
  ): Promise<MatchResponse> {
    const response = await fetch(
      `${this.backendBaseUrl}/api/games/${this.gameDefinitionId}/matches/${matchId}/commands`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ commandName, arguments: args ?? null })
      }
    );

    return this.readJson(response);
  }

  private async readJson(response: Response): Promise<MatchResponse> {
    const payload = await response.json();

    if (!response.ok) {
      throw new Error(payload.message ?? "Unexpected server error.");
    }

    return payload as MatchResponse;
  }
}

class CanvasBoardRenderer {
  private readonly context: CanvasRenderingContext2D;
  private readonly radius = 56;
  private readonly origin = { x: 500, y: 380 };
  private hexCenters = new Map<string, { x: number; y: number }>();

  constructor(private readonly canvas: HTMLCanvasElement) {
    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error("Canvas 2D context is not available.");
    }

    this.context = context;
  }

  render(match: MatchResponse, selectedUnit: SelectedUnit): void {
    const ctx = this.context;
    const state = match.state;

    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    this.hexCenters = new Map();

    for (let r = -state.board.radius; r <= state.board.radius; r += 1) {
      const minQ = Math.max(-state.board.radius, -r - state.board.radius);
      const maxQ = Math.min(state.board.radius, -r + state.board.radius);

      for (let q = minQ; q <= maxQ; q += 1) {
        const coordinate = { q, r };
        const center = this.toPixel(coordinate);
        this.hexCenters.set(this.key(coordinate), center);
        this.drawHex(state, coordinate, center, selectedUnit);
      }
    }

    for (const unit of state.units) {
      const center = this.hexCenters.get(this.key(unit.position));
      if (center) {
        this.drawUnit(unit, center, unit.id === selectedUnit?.id);
      }
    }
  }

  tryGetCoordinateAt(x: number, y: number): Coordinate | null {
    for (const [key, center] of this.hexCenters.entries()) {
      const dx = x - center.x;
      const dy = y - center.y;
      if (Math.sqrt(dx * dx + dy * dy) <= this.radius * 0.92) {
        const [q, r] = key.split(",").map(Number);
        return { q, r };
      }
    }

    return null;
  }

  private drawHex(
    state: KingOfTheHillState,
    coordinate: Coordinate,
    center: { x: number; y: number },
    selectedUnit: SelectedUnit
  ): void {
    const ctx = this.context;
    const isObjective =
      coordinate.q === state.objectiveCoordinate.q &&
      coordinate.r === state.objectiveCoordinate.r;
    const isSelectedDestination =
      selectedUnit !== null &&
      this.distance(selectedUnit.position, coordinate) === 1 &&
      !state.units.some(
        unit => unit.position.q === coordinate.q && unit.position.r === coordinate.r
      );

    const points = this.hexPoints(center);

    ctx.beginPath();
    ctx.moveTo(points[0].x, points[0].y);
    for (const point of points.slice(1)) {
      ctx.lineTo(point.x, point.y);
    }
    ctx.closePath();

    if (isObjective) {
      ctx.fillStyle = "#f7d47d";
    } else if (isSelectedDestination) {
      ctx.fillStyle = "#f2e3b6";
    } else {
      ctx.fillStyle = "#f9f4ea";
    }

    ctx.fill();
    ctx.lineWidth = isObjective ? 4 : 2;
    ctx.strokeStyle = isObjective ? "#aa3a2a" : "#7b664e";
    ctx.stroke();

    ctx.fillStyle = "#5c4a35";
    ctx.font = "14px Segoe UI";
    ctx.textAlign = "center";
    ctx.fillText(`${coordinate.q},${coordinate.r}`, center.x, center.y + this.radius * 0.85);
  }

  private drawUnit(unit: Unit, center: { x: number; y: number }, isSelected: boolean): void {
    const ctx = this.context;
    ctx.beginPath();
    ctx.arc(center.x, center.y, this.radius * 0.38, 0, Math.PI * 2);
    ctx.fillStyle = unit.ownerPlayerId === "P1" ? "#2d7f6b" : "#a33e2a";
    ctx.fill();
    ctx.lineWidth = isSelected ? 5 : 2;
    ctx.strokeStyle = isSelected ? "#171311" : "#f7efe2";
    ctx.stroke();

    ctx.fillStyle = "#ffffff";
    ctx.font = "bold 17px Segoe UI";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(unit.id.toUpperCase(), center.x, center.y);
  }

  private toPixel(coordinate: Coordinate): { x: number; y: number } {
    const x = this.radius * Math.sqrt(3) * (coordinate.q + coordinate.r / 2) + this.origin.x;
    const y = this.radius * 1.5 * coordinate.r + this.origin.y;
    return { x, y };
  }

  private hexPoints(center: { x: number; y: number }): Array<{ x: number; y: number }> {
    const points: Array<{ x: number; y: number }> = [];
    for (let i = 0; i < 6; i += 1) {
      const angle = (Math.PI / 180) * (60 * i - 30);
      points.push({
        x: center.x + this.radius * Math.cos(angle),
        y: center.y + this.radius * Math.sin(angle)
      });
    }
    return points;
  }

  private key(coordinate: Coordinate): string {
    return `${coordinate.q},${coordinate.r}`;
  }

  private distance(a: Coordinate, b: Coordinate): number {
    const sA = -a.q - a.r;
    const sB = -b.q - b.r;
    return Math.max(Math.abs(a.q - b.q), Math.abs(a.r - b.r), Math.abs(sA - sB));
  }
}

const config = window.kingOfTheHillConfig ?? {};
const client = new KingOfTheHillClient(
  (config.backendBaseUrl ?? "http://localhost:5091").replace(/\/$/, ""),
  config.gameDefinitionId ?? "king-of-the-hill"
);

const canvas = document.querySelector<HTMLCanvasElement>("#game-canvas");
const metaPanel = document.querySelector<HTMLDivElement>("#match-meta");
const selectionPanel = document.querySelector<HTMLDivElement>("#selection-panel");
const logPanel = document.querySelector<HTMLDivElement>("#log-panel");
const newMatchButton = document.querySelector<HTMLButtonElement>("#new-match-button");
const passButton = document.querySelector<HTMLButtonElement>("#pass-button");

if (!canvas || !metaPanel || !selectionPanel || !logPanel || !newMatchButton || !passButton) {
  throw new Error("The web tool could not find the required DOM elements.");
}

const renderer = new CanvasBoardRenderer(canvas);
let currentMatch: MatchResponse | null = null;
let selectedUnit: SelectedUnit = null;

async function startNewMatch(): Promise<void> {
  currentMatch = await client.createMatch();
  selectedUnit = null;
  render();
  pushLog(currentMatch.lastMessage);
}

function render(): void {
  if (!currentMatch) {
    return;
  }

  renderer.render(currentMatch, selectedUnit);
  renderMeta(currentMatch);
  renderSelection(currentMatch.state, selectedUnit);
}

function renderMeta(match: MatchResponse): void {
  const state = match.state;
  const scores = Object.entries(state.controlScores)
    .map(([playerId, value]) => `${playerId}: ${value}`)
    .join(" | ");

  metaPanel.innerHTML = "";
  metaPanel.append(
    makeMetaRow("Game", match.gameDefinitionId),
    makeMetaRow("Turn", state.turnNumber.toString()),
    makeMetaRow("Current Player", state.currentPlayer.displayName),
    makeMetaRow("Score", scores),
    makeMetaRow("Winner", state.winnerPlayerId ?? "-")
  );
}

function renderSelection(state: KingOfTheHillState, unit: SelectedUnit): void {
  selectionPanel.innerHTML = "";

  if (!unit) {
    selectionPanel.append(makeMetaRow("Selected Unit", "None"));
    selectionPanel.append(
      makeMetaRow("Hint", `Click one of ${state.currentPlayerId}'s units.`)
    );
    return;
  }

  selectionPanel.append(
    makeMetaRow("Selected Unit", unit.id),
    makeMetaRow("Owner", unit.ownerPlayerId),
    makeMetaRow("Position", `${unit.position.q}, ${unit.position.r}`)
  );
}

function makeMetaRow(label: string, value: string): HTMLDivElement {
  const row = document.createElement("div");
  row.className = "meta-row";
  row.innerHTML = `<strong>${label}</strong><span>${value}</span>`;
  return row;
}

function pushLog(message: string): void {
  const entry = document.createElement("div");
  entry.className = "log-entry";
  entry.textContent = message;
  logPanel.prepend(entry);
}

canvas.addEventListener("click", async event => {
  if (!currentMatch || currentMatch.state.isCompleted) {
    return;
  }

  const bounds = canvas.getBoundingClientRect();
  const scaleX = canvas.width / bounds.width;
  const scaleY = canvas.height / bounds.height;
  const x = (event.clientX - bounds.left) * scaleX;
  const y = (event.clientY - bounds.top) * scaleY;

  const coordinate = renderer.tryGetCoordinateAt(x, y);
  if (!coordinate) {
    return;
  }

  const clickedUnit = currentMatch.state.units.find(
    unit => unit.position.q === coordinate.q && unit.position.r === coordinate.r
  );

  if (clickedUnit && clickedUnit.ownerPlayerId === currentMatch.state.currentPlayerId) {
    selectedUnit = clickedUnit;
    render();
    return;
  }

  if (!selectedUnit) {
    pushLog("Select one of the active player's units before choosing a destination.");
    return;
  }

  try {
    currentMatch = await client.sendCommand(currentMatch.matchId, "move", {
      unitId: selectedUnit.id,
      q: coordinate.q.toString(),
      r: coordinate.r.toString()
    });
    selectedUnit = null;
    render();
    pushLog(currentMatch.lastMessage);
  } catch (error) {
    pushLog((error as Error).message);
  }
});

newMatchButton.addEventListener("click", async () => {
  await startNewMatch();
});

passButton.addEventListener("click", async () => {
  if (!currentMatch || currentMatch.state.isCompleted) {
    return;
  }

  try {
    currentMatch = await client.sendCommand(currentMatch.matchId, "pass");
    selectedUnit = null;
    render();
    pushLog(currentMatch.lastMessage);
  } catch (error) {
    pushLog((error as Error).message);
  }
});

startNewMatch().catch(error => {
  pushLog((error as Error).message);
});
