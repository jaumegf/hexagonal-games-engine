class KingOfTheHillClient {
  constructor(backendBaseUrl, gameDefinitionId) {
    this.backendBaseUrl = backendBaseUrl;
    this.gameDefinitionId = gameDefinitionId;
  }
  async createMatch(setup) {
    const response = await fetch(
      `${this.backendBaseUrl}/api/games/${this.gameDefinitionId}/matches`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          player1Controller: setup.player1,
          player2Controller: setup.player2
        })
      }
    );
    return this.readJson(response);
  }
  async sendCommand(matchId, commandName, args) {
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
  async executeAutomatedTurn(matchId) {
    const response = await fetch(
      `${this.backendBaseUrl}/api/games/${this.gameDefinitionId}/matches/${matchId}/automated-turn`,
      {
        method: "POST"
      }
    );
    return this.readJson(response);
  }
  async getMatch(matchId) {
    const response = await fetch(
      `${this.backendBaseUrl}/api/games/${this.gameDefinitionId}/matches/${matchId}`,
      {
        method: "GET"
      }
    );
    return this.readJson(response);
  }
  async importMatch(state, lastMessage) {
    const response = await fetch(
      `${this.backendBaseUrl}/api/games/${this.gameDefinitionId}/matches/import`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          state,
          lastMessage
        })
      }
    );
    return this.readJson(response);
  }
  async readJson(response) {
    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.message ?? "Unexpected server error.");
    }
    return payload;
  }
}
class CanvasBoardRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.radius = 45;
    this.tilePalette = {
      neutralFill: "#f9f4ea",
      neutralStroke: "#7b664e",
      ringOneFill: "#f5ecd1",
      ringOneStroke: "#c8ab63",
      dominantRingOneFillP1: "#e2f2ea",
      dominantRingOneFillP2: "#f6e3dd",
      dominantObjectiveFillP1: "#cfe8dc",
      dominantObjectiveFillP2: "#efd0c7",
      gameplayNeutralStroke: "#c9c1b3",
      gameplayRingOneStroke: "#d8cfbf",
      selectedFill: "#f2df5a",
      selectedStroke: "#b88900",
      selectableStrokeP1: "rgba(95, 159, 141, 0.6)",
      selectableStrokeP2: "rgba(195, 122, 104, 0.6)",
      objectiveFill: "#f1c95f",
      objectiveStroke: "#c59f53",
      objectiveActiveFill: "#dff0a8",
      objectiveActiveStroke: "#6f9a2f",
      objectiveBlockedFill: "#e9dfcf",
      objectiveBlockedStroke: "#aa3a2a",
      blockedFill: "#f2ddd7",
      blockedStroke: "#aa3a2a",
      moveFillP1: "#dff3eb",
      moveStrokeP1: "#5f9f8d",
      moveFillP2: "#f7e2db",
      moveStrokeP2: "#c37a68",
      mergeFill: "#d6efe8",
      mergeStroke: "#2d7f6b",
      attackFill: "#d9f0c8",
      attackStroke: "#4b8f2f"
    };
    this.hexCenters = new Map();
    this.tileLayout = new Map();
    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error("Canvas 2D context is not available.");
    }
    this.context = context;
  }
  render(match, selectedUnit, renderMode, hoveredCoordinate, objectiveSiegePulse) {
    const ctx = this.context;
    const state = match.state;
    const emphasizeSelectableUnits = state.currentPlayer.kind === "Human" && selectedUnit === null && !match.state.isCompleted;
    const activeHoveredCoordinate = selectedUnit !== null && hoveredCoordinate !== null && this.canMoveToCoordinate(state, selectedUnit, hoveredCoordinate) ? hoveredCoordinate : null;
    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    this.hexCenters = new Map();
    this.tileLayout = new Map();
    const rows = this.buildRows(state.board.coordinates);
    const dominantZonePlayerId = this.getDominantZonePlayerId(state);
    for (const row of rows) {
      for (const item of row.coordinates) {
        this.hexCenters.set(this.key(item.coordinate), item.center);
        this.tileLayout.set(this.key(item.coordinate), { q: item.coordinate.q, r: item.coordinate.r });
      }
    }
    const highlightedTiles = [];
    for (const row of rows) {
      for (const item of row.coordinates) {
        const isObjective = item.coordinate.q === state.objectiveCoordinate.q && item.coordinate.r === state.objectiveCoordinate.r;
        const highlightKind = selectedUnit === null ? "none" : this.getHighlightKind(state, selectedUnit, item.coordinate);
        const isSelectedTile = selectedUnit !== null && item.coordinate.q === selectedUnit.position.q && item.coordinate.r === selectedUnit.position.r;
        const isSelectableTile = emphasizeSelectableUnits && state.units.some(
          (unit) => unit.ownerPlayerId === state.currentPlayerId && unit.position.q === item.coordinate.q && unit.position.r === item.coordinate.r
        );
        const isHoveredTile = activeHoveredCoordinate !== null && item.coordinate.q === activeHoveredCoordinate.q && item.coordinate.r === activeHoveredCoordinate.r;
        const blockedEdgeIndexes = selectedUnit === null || (highlightKind !== "objective-blocked" && highlightKind !== "blocked") ? [] : this.getBlockedEdgeIndexes(state, selectedUnit, item.coordinate, item.center);
        this.drawHex(
          item.coordinate,
          item.center,
          isObjective,
          highlightKind,
          blockedEdgeIndexes,
          isSelectedTile,
          isSelectableTile,
          isHoveredTile,
          state,
          state.controlScores,
          state.currentPlayerId,
          dominantZonePlayerId,
          renderMode,
          objectiveSiegePulse
        );
        if (highlightKind !== "none") {
          highlightedTiles.push({
            coordinate: item.coordinate,
            center: item.center,
            highlightKind,
            isObjective,
            blockedEdgeIndexes,
            isSelectedTile,
            isSelectableTile
          });
        }
      }
    }
    const orderedHighlightedTiles = highlightedTiles.slice().sort(
      (left, right) => Number(left.isObjective) - Number(right.isObjective) || Number(left.highlightKind === "objective-attack") - Number(right.highlightKind === "objective-attack") || Number(left.highlightKind === "objective-blocked") - Number(right.highlightKind === "objective-blocked")
    );
    for (const tile of orderedHighlightedTiles) {
      this.drawHex(
        tile.coordinate,
        tile.center,
        tile.isObjective,
        tile.highlightKind,
        tile.blockedEdgeIndexes,
        tile.isSelectedTile,
        tile.isSelectableTile,
        activeHoveredCoordinate !== null && tile.coordinate.q === activeHoveredCoordinate.q && tile.coordinate.r === activeHoveredCoordinate.r,
        state,
        state.controlScores,
        state.currentPlayerId,
        dominantZonePlayerId,
        renderMode,
        objectiveSiegePulse
      );
    }
    if (selectedUnit !== null) {
      const selectedCenter = this.hexCenters.get(this.key(selectedUnit.position));
      if (selectedCenter) {
        const isObjective = selectedUnit.position.q === state.objectiveCoordinate.q && selectedUnit.position.r === state.objectiveCoordinate.r;
        this.drawHex(
          selectedUnit.position,
          selectedCenter,
          isObjective,
          "none",
          [],
          true,
          false,
          false,
          state,
          state.controlScores,
          state.currentPlayerId,
          dominantZonePlayerId,
          renderMode,
          objectiveSiegePulse
        );
      }
    }
    for (const unit of state.units) {
      const center = this.hexCenters.get(this.key(unit.position));
      if (center) {
        this.drawUnit(
          unit,
          center,
          unit.id === (selectedUnit == null ? void 0 : selectedUnit.id),
          unit.ownerPlayerId === state.currentPlayerId,
          emphasizeSelectableUnits,
          renderMode,
          activeHoveredCoordinate
        );
      }
    }
    if (renderMode === "gameplay" && selectedUnit !== null && activeHoveredCoordinate !== null) {
      const hoveredUnit = state.units.find(
        (unit) => unit.position.q === activeHoveredCoordinate.q && unit.position.r === activeHoveredCoordinate.r
      ) ?? null;
      const mergedStrength = selectedUnit.strength + (hoveredUnit?.strength ?? 0);
      if (
        hoveredUnit !== null &&
        hoveredUnit.ownerPlayerId === selectedUnit.ownerPlayerId &&
        !this.isDefenderUnit(selectedUnit) &&
        !this.isDefenderUnit(hoveredUnit) &&
        mergedStrength <= 3
      ) {
        const hoveredCenter = this.hexCenters.get(this.key(activeHoveredCoordinate));
        if (hoveredCenter) {
          this.drawMergePreview(
            hoveredCenter,
            selectedUnit.strength,
            hoveredUnit.strength,
            mergedStrength
          );
        }
      }
    }
  }
  tryGetCoordinateAt(x, y) {
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
  drawHex(coordinate, center, isObjective, highlightKind, blockedEdgeIndexes, isSelectedTile, isSelectableTile, isHoveredTile, state, controlScores, currentPlayerId, dominantZonePlayerId, renderMode, objectiveSiegePulse) {
    const ctx = this.context;
    const isRingOne = this.hexDistance(coordinate, { q: 0, r: 0 }) === 1;
    const points = this.hexPoints(center);
    ctx.beginPath();
    ctx.moveTo(points[0].x, points[0].y);
    for (const point of points.slice(1)) {
      ctx.lineTo(point.x, point.y);
    }
    ctx.closePath();
    ctx.fillStyle = this.getFillColor(isObjective, isRingOne, highlightKind, isSelectedTile, currentPlayerId, dominantZonePlayerId, renderMode);
    ctx.fill();
    if (isHoveredTile) {
      ctx.fillStyle = "rgba(58, 43, 28, 0.08)";
      ctx.fill();
    }
    const isGameplaySelectable = renderMode === "gameplay" && isSelectableTile && !isSelectedTile;
    ctx.lineWidth = isObjective ? highlightKind === "objective" ? isHoveredTile ? 6 : 5 : isHoveredTile ? 5 : 4 : isSelectedTile ? 4.5 : isGameplaySelectable ? 2.5 : isHoveredTile ? 4 : 2;
    ctx.strokeStyle = isSelectedTile ? this.getSelectedStrokeColor(currentPlayerId, renderMode) : isGameplaySelectable ? this.getSelectableStrokeColor(currentPlayerId, renderMode) : this.getStrokeColor(isObjective, isRingOne, highlightKind, currentPlayerId, renderMode);
    ctx.setLineDash(isObjective ? [8, 6] : isGameplaySelectable ? [7, 5] : []);
    ctx.stroke();
    ctx.setLineDash([]);
    if (isObjective && highlightKind !== "none") {
      const outlineWidth = isObjective ? 6 : 5;
      const outlineOffset = isObjective ? 5 : 4;
      this.drawOutline(
        points,
        this.getStrokeColor(isObjective, isRingOne, highlightKind, currentPlayerId, renderMode),
        outlineWidth,
        outlineOffset
      );
    }
    if (blockedEdgeIndexes.length > 0) {
      const outlineOffset = isObjective ? 5 : 4;
      const outlineWidth = isObjective ? 6 : 5;
      this.drawOutline(points, this.tilePalette.blockedStroke, outlineWidth, outlineOffset, blockedEdgeIndexes);
    }
    if (isSelectedTile) {
      this.drawOutline(
        points,
        this.getSelectedStrokeColor(currentPlayerId, renderMode),
        renderMode === "gameplay" ? 1.75 : 2.5,
        renderMode === "gameplay" ? 0.8 : 1.5
      );
    }
    if (renderMode === "gameplay" && isSelectableTile && !isSelectedTile) {
      this.drawOutline(
        points,
        this.getSelectableStrokeColor(currentPlayerId, renderMode),
        1.1,
        0.35
      );
    }
    if (isObjective && objectiveSiegePulse > 0) {
      const siegeStroke = `rgba(197, 159, 83, ${0.45 + objectiveSiegePulse * 0.45})`;
      const siegeFill = `rgba(245, 224, 163, ${0.1 + objectiveSiegePulse * 0.12})`;
      this.drawOutline(points, siegeStroke, 2 + objectiveSiegePulse * 2, 1.5 + objectiveSiegePulse * 1.25);
      ctx.beginPath();
      ctx.moveTo(points[0].x, points[0].y);
      for (const point of points.slice(1)) {
        ctx.lineTo(point.x, point.y);
      }
      ctx.closePath();
      ctx.fillStyle = siegeFill;
      ctx.fill();
    }
    if (renderMode === "debug") {
      ctx.fillStyle = "#5c4a35";
      ctx.font = "12px Segoe UI";
      ctx.textAlign = "center";
      ctx.fillText(`${coordinate.q},${coordinate.r}`, center.x, center.y + this.radius * 0.85);
    }
  }
  drawUnit(unit, center, isSelected, isActivePlayerUnit, emphasizeSelectableUnits, renderMode, hoveredCoordinate) {
    if (renderMode === "gameplay") {
      this.drawGameplayUnit(unit, center, isSelected, isActivePlayerUnit, emphasizeSelectableUnits, hoveredCoordinate);
      return;
    }
    const ctx = this.context;
    const shouldEmphasize = emphasizeSelectableUnits && isActivePlayerUnit && !isSelected;
    if (shouldEmphasize) {
      ctx.beginPath();
      ctx.arc(center.x, center.y, this.radius * 0.5, 0, Math.PI * 2);
      ctx.fillStyle = unit.ownerPlayerId === "P1" ? "rgba(168, 227, 212, 0.35)" : "rgba(241, 178, 157, 0.35)";
      ctx.fill();
      ctx.beginPath();
      ctx.arc(center.x, center.y, this.radius * 0.56, 0, Math.PI * 2);
      ctx.lineWidth = 3;
      ctx.strokeStyle = unit.ownerPlayerId === "P1" ? "#7dd6c0" : "#ef9b85";
      ctx.stroke();
    }
    ctx.beginPath();
    ctx.arc(center.x, center.y, this.radius * 0.38, 0, Math.PI * 2);
    ctx.fillStyle = unit.ownerPlayerId === "P1" ? "#2d7f6b" : "#a33e2a";
    ctx.fill();
    ctx.lineWidth = isSelected ? 3 : isActivePlayerUnit ? 4 : 2;
    ctx.strokeStyle = isSelected ? this.tilePalette.selectedStroke : isActivePlayerUnit ? unit.ownerPlayerId === "P1" ? "#a8e3d4" : "#f1b29d" : "#f7efe2";
    ctx.stroke();
    ctx.fillStyle = "#ffffff";
    ctx.font = "bold 14px Segoe UI";
    ctx.textAlign = "center";
    ctx.textBaseline = "alphabetic";
    ctx.fillText(unit.id.toUpperCase(), center.x, center.y - 2);
    if (unit.strength > 1) {
      ctx.font = "bold 11px Segoe UI";
      ctx.fillText(`S${unit.strength}`, center.x, center.y + 13);
    }
  }
  drawGameplayUnit(unit, center, isSelected, isActivePlayerUnit, emphasizeSelectableUnits, hoveredCoordinate) {
    const ctx = this.context;
    const strength = Math.max(1, unit.strength);
    const dotRadius = Math.min(this.radius * 0.12, 4.8);
    const gridColumns = Math.min(2, strength);
    const gridRows = Math.ceil(strength / 2);
    const columnGap = dotRadius * 1.35;
    const rowGap = dotRadius * 1.2;
    const dotGridWidth = gridColumns * dotRadius * 2 + Math.max(0, gridColumns - 1) * columnGap;
    const dotGridHeight = gridRows * dotRadius * 2 + Math.max(0, gridRows - 1) * rowGap;
    const badgeWidth = Math.max(this.radius * 0.8, dotGridWidth + 18);
    const badgeHeight = Math.max(this.radius * 0.68, dotGridHeight + 14);
    const badgeX = center.x - badgeWidth / 2;
    const badgeY = center.y - badgeHeight / 2;
    const badgeRadius = Math.min(7, badgeHeight / 3.2);
    const isDefender = this.isDefenderUnit(unit);
    const unitColor = unit.ownerPlayerId === "P1" ? "#2d7f6b" : "#a33e2a";
    const unitAccent = unit.ownerPlayerId === "P1" ? "#a8e3d4" : "#f1b29d";
    ctx.beginPath();
    ctx.roundRect(badgeX, badgeY, badgeWidth, badgeHeight, badgeRadius);
    ctx.fillStyle = isDefender ? unit.ownerPlayerId === "P1" ? "rgba(150, 209, 194, 0.36)" : "rgba(229, 168, 150, 0.36)" : "rgba(255, 249, 238, 0.18)";
    ctx.fill();
    for (let index = 0; index < strength; index += 1) {
      let currentX;
      let currentY;
      if (strength === 3) {
        if (index === 0) {
          currentX = center.x;
          currentY = center.y - (dotRadius + rowGap / 2);
        } else {
          const bottomRowWidth = dotRadius * 4 + columnGap;
          currentX = center.x - bottomRowWidth / 2 + dotRadius + (index - 1) * (dotRadius * 2 + columnGap);
          currentY = center.y + (dotRadius + rowGap / 2);
        }
      } else {
        const columnIndex = index % 2;
        const rowIndex = Math.floor(index / 2);
        const dotsInRow = rowIndex === gridRows - 1 && strength % 2 === 1 && gridColumns === 2 ? 1 : gridColumns;
        const rowWidth = dotsInRow * dotRadius * 2 + Math.max(0, dotsInRow - 1) * columnGap;
        currentX = center.x - rowWidth / 2 + dotRadius + columnIndex * (dotRadius * 2 + columnGap);
        currentY = center.y - dotGridHeight / 2 + dotRadius + rowIndex * (dotRadius * 2 + rowGap);
      }
      ctx.beginPath();
      ctx.arc(currentX, currentY, dotRadius, 0, Math.PI * 2);
      ctx.fillStyle = unitColor;
      ctx.fill();
      ctx.beginPath();
      ctx.arc(currentX - dotRadius * 0.22, currentY - dotRadius * 0.24, dotRadius * 0.42, 0, Math.PI * 2);
      ctx.fillStyle = unitAccent;
      ctx.globalAlpha = 0.72;
      ctx.fill();
      ctx.globalAlpha = 1;
    }
    if (isSelected) {
      const arrowCount = unit.strength === 1 ? 2 : 1;
      const directionAngle = this.getMovementArrowAngle(unit, hoveredCoordinate, isSelected);
      const arrowSpacing = arrowCount === 2 ? 12 : 0;
      const arrowCenterY = center.y - this.radius * 0.96;
      const hoveredDistance = hoveredCoordinate === null ? null : this.hexDistance(unit.position, hoveredCoordinate);
      for (let index = 0; index < arrowCount; index += 1) {
        const offsetX = arrowCount === 2 ? index === 0 ? -arrowSpacing / 2 : arrowSpacing / 2 : 0;
        const arrowOpacity = this.getMovementArrowOpacity(arrowCount, hoveredDistance, index);
        this.drawMovementArrow(
          { x: center.x + offsetX, y: arrowCenterY },
          directionAngle,
          unit.ownerPlayerId,
          true,
          arrowOpacity
        );
      }
    }
  }
  isDefenderUnit(unit) {
    return unit.id.endsWith("T") || unit.id.endsWith("V") || unit.id.endsWith("X");
  }
  getUnitRole(unit) {
    if (this.isDefenderUnit(unit)) {
      return "defender";
    }
    if (unit.strength === 1) {
      return "single";
    }
    if (unit.strength === 2) {
      return "double";
    }
    return "attacker";
  }
  canRoleOccupyCoordinate(unit, coordinate) {
    const ring = this.hexDistance(coordinate, { q: 0, r: 0 });
    const role = this.getUnitRole(unit);
    switch (role) {
      case "single":
        return true;
      case "double":
        return ring >= 1;
      case "defender":
      case "attacker":
        return ring >= 2;
      default:
        return false;
    }
  }
  drawMovementArrow(center, angle, ownerPlayerId, isSelected, opacity) {
    const ctx = this.context;
    const arrowStroke = ownerPlayerId === "P1" ? `rgba(45, 127, 107, ${opacity})` : `rgba(163, 62, 42, ${opacity})`;
    ctx.save();
    ctx.translate(center.x, center.y);
    ctx.rotate(angle);
    ctx.beginPath();
    ctx.moveTo(-6, 4);
    ctx.lineTo(0, -4);
    ctx.lineTo(6, 4);
    ctx.lineWidth = 2.2;
    ctx.lineJoin = "round";
    ctx.lineCap = "round";
    ctx.strokeStyle = arrowStroke;
    if (isSelected) {
      ctx.shadowColor = "transparent";
      ctx.shadowBlur = 0;
    }
    ctx.stroke();
    ctx.restore();
  }
  getMovementArrowOpacity(arrowCount, hoveredDistance, arrowIndex) {
    if (arrowCount === 1) {
      return hoveredDistance === null ? 0.28 : 0.42;
    }
    if (hoveredDistance === 1) {
      return arrowIndex === 0 ? 0.42 : 0;
    }
    if (hoveredDistance !== null && hoveredDistance >= 2) {
      return 0.42;
    }
    return 0.28;
  }
  drawMergePreview(center, sourceStrength, targetStrength, resultStrength) {
    const ctx = this.context;
    const barHeight = 6;
    const barSpacing = 2.4;
    const groupSpacing = 6;
    const plusWidth = 6;
    const equalsWidth = 8;
    const previewY = center.y - this.radius * 0.42;
    const totalWidth = this.getBarGroupWidth(sourceStrength, barSpacing) + groupSpacing + plusWidth + groupSpacing + this.getBarGroupWidth(targetStrength, barSpacing) + groupSpacing + equalsWidth + groupSpacing + this.getBarGroupWidth(resultStrength, barSpacing);
    let cursorX = center.x - totalWidth / 2;
    cursorX = this.drawBarGroup(cursorX, previewY, sourceStrength, barHeight, barSpacing);
    cursorX += groupSpacing;
    cursorX = this.drawPreviewOperator(cursorX, previewY, "+", plusWidth);
    cursorX += groupSpacing;
    cursorX = this.drawBarGroup(cursorX, previewY, targetStrength, barHeight, barSpacing);
    cursorX += groupSpacing;
    cursorX = this.drawPreviewOperator(cursorX, previewY, "=", equalsWidth);
    cursorX += groupSpacing;
    this.drawBarGroup(cursorX, previewY, resultStrength, barHeight, barSpacing);
  }
  getBarGroupWidth(strength, spacing) {
    if (strength <= 0) {
      return 0;
    }
    return strength * 4 + (strength - 1) * spacing;
  }
  drawBarGroup(startX, centerY, strength, barHeight, spacing) {
    const ctx = this.context;
    let currentX = startX;
    const dotRadius = 2;
    ctx.save();
    ctx.fillStyle = "#8b7355";
    for (let index = 0; index < strength; index += 1) {
      ctx.beginPath();
      ctx.arc(currentX + dotRadius, centerY, dotRadius, 0, Math.PI * 2);
      ctx.fill();
      ctx.beginPath();
      ctx.arc(currentX + dotRadius - 0.45, centerY - 0.45, dotRadius * 0.42, 0, Math.PI * 2);
      ctx.fillStyle = "#d9c4a4";
      ctx.globalAlpha = 0.7;
      ctx.fill();
      ctx.globalAlpha = 1;
      ctx.fillStyle = "#8b7355";
      currentX += dotRadius * 2 + spacing;
    }
    ctx.restore();
    return currentX - spacing;
  }
  drawPreviewOperator(startX, centerY, symbol, width) {
    const ctx = this.context;
    ctx.save();
    ctx.fillStyle = "#7b664e";
    ctx.font = "bold 9px Consolas";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(symbol, startX + width / 2, centerY + 0.5);
    ctx.restore();
    return startX + width;
  }
  getMovementArrowAngle(unit, hoveredCoordinate, isSelected) {
    if (isSelected && hoveredCoordinate !== null) {
      const originCenter = this.hexCenters.get(this.key(unit.position));
      const hoveredCenter = this.hexCenters.get(this.key(hoveredCoordinate));
      if (originCenter && hoveredCenter) {
        return Math.atan2(
          hoveredCenter.y - originCenter.y,
          hoveredCenter.x - originCenter.x
        ) + Math.PI / 2;
      }
    }
    return unit.ownerPlayerId === "P1" ? 0 : Math.PI;
  }
  hexPoints(center) {
    const points = [];
    for (let i = 0; i < 6; i += 1) {
      const angle = Math.PI / 180 * (60 * i - 30);
      points.push({
        x: center.x + this.radius * Math.cos(angle),
        y: center.y + this.radius * Math.sin(angle)
      });
    }
    return points;
  }
  hexOutlinePoints(center, offset) {
    const points = [];
    const outlineRadius = this.radius + offset;
    for (let i = 0; i < 6; i += 1) {
      const angle = Math.PI / 180 * (60 * i - 30);
      points.push({
        x: center.x + outlineRadius * Math.cos(angle),
        y: center.y + outlineRadius * Math.sin(angle)
      });
    }
    return points;
  }
  drawOutline(basePoints, strokeStyle, lineWidth, offset, edgeIndexes) {
    const ctx = this.context;
    const center = this.getCenterFromPoints(basePoints);
    const outlinePoints = this.hexOutlinePoints(center, offset);
    const indexes = edgeIndexes ?? [0, 1, 2, 3, 4, 5];
    ctx.lineWidth = lineWidth;
    ctx.strokeStyle = strokeStyle;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    for (const edgeIndex of indexes) {
      const from = outlinePoints[edgeIndex];
      const to = outlinePoints[(edgeIndex + 1) % outlinePoints.length];
      ctx.beginPath();
      ctx.moveTo(from.x, from.y);
      ctx.lineTo(to.x, to.y);
      ctx.stroke();
    }
    ctx.lineCap = "butt";
    ctx.lineJoin = "miter";
  }
  getCenterFromPoints(points) {
    const total = points.reduce(
      (accumulator, point) => ({
        x: accumulator.x + point.x,
        y: accumulator.y + point.y
      }),
      { x: 0, y: 0 }
    );
    return {
      x: total.x / points.length,
      y: total.y / points.length
    };
  }
  buildRows(coordinates) {
    const groupedRows = new Map();
    for (const coordinate of coordinates) {
      const row = groupedRows.get(coordinate.r) ?? [];
      row.push(coordinate);
      groupedRows.set(coordinate.r, row);
    }
    const rows = Array.from(groupedRows.entries()).sort(([left], [right]) => left - right).map(([r, rowCoordinates]) => ({
      r,
      coordinates: rowCoordinates.sort((left, right) => left.q - right.q)
    }));
    const rowSpacing = this.radius * 1.5;
    const columnSpacing = this.radius * Math.sqrt(3);
    const projectedColumns = coordinates.map((coordinate) => coordinate.q + coordinate.r / 2);
    const minProjectedColumn = Math.min(...projectedColumns);
    const maxProjectedColumn = Math.max(...projectedColumns);
    const projectedColumnCenter = (minProjectedColumn + maxProjectedColumn) / 2;
    const rowValues = rows.map((row) => row.r);
    const minRow = Math.min(...rowValues);
    const maxRow = Math.max(...rowValues);
    const rowCenter = (minRow + maxRow) / 2;
    const horizontalCenter = this.canvas.width / 2;
    const verticalCenter = this.canvas.height / 2 - this.radius * 0.12;
    return rows.map((row) => {
      return {
        r: row.r,
        coordinates: row.coordinates.map((coordinate) => ({
          coordinate,
          x: coordinate.q + coordinate.r / 2,
          center: {
            x: horizontalCenter + (coordinate.q + coordinate.r / 2 - projectedColumnCenter) * columnSpacing,
            y: verticalCenter + (coordinate.r - rowCenter) * rowSpacing
          }
        }))
      };
    });
  }
  key(coordinate) {
    return `${coordinate.q},${coordinate.r}`;
  }
  hexDistance(a, b) {
    const deltaQ = Math.abs(a.q - b.q);
    const deltaR = Math.abs(a.r - b.r);
    const deltaS = Math.abs(-a.q - a.r - (-b.q - b.r));
    return Math.max(deltaQ, deltaR, deltaS);
  }
  areAdjacent(a, b) {
    return this.hexDistance(a, b) === 1;
  }
  getReachableCoordinates(origin, maxDepth) {
    if (maxDepth <= 0 || !this.tileLayout.has(this.key(origin))) {
      return [];
    }
    const visited = new Set([this.key(origin)]);
    const depths = new Map([[this.key(origin), 0]]);
    const queue = [origin];
    while (queue.length > 0) {
      const current = queue.shift();
      const currentKey = this.key(current);
      const currentDepth = depths.get(currentKey) ?? 0;
      if (currentDepth >= maxDepth) {
        continue;
      }
      for (const neighbor of this.getAdjacentCoordinates(current)) {
        const neighborKey = this.key(neighbor);
        if (visited.has(neighborKey)) {
          continue;
        }
        visited.add(neighborKey);
        depths.set(neighborKey, currentDepth + 1);
        queue.push(neighbor);
      }
    }
    return Array.from(visited).filter((key) => key !== this.key(origin)).map((key) => {
      const [q, r] = key.split(",").map(Number);
      return { q, r };
    });
  }
  getAdjacentCoordinates(origin) {
    const adjacent = [];
    for (const [key] of this.tileLayout.entries()) {
      const [q, r] = key.split(",").map(Number);
      const candidate = { q, r };
      if (this.areAdjacent(origin, candidate)) {
        adjacent.push(candidate);
      }
    }
    return adjacent;
  }
  canMoveToCoordinate(state, selectedUnit, coordinate) {
    if (selectedUnit.position.q === coordinate.q && selectedUnit.position.r === coordinate.r) {
      return false;
    }
    if (!this.hasTraversablePath(state, selectedUnit, coordinate)) {
      return false;
    }
    if (!this.canRoleOccupyCoordinate(selectedUnit, coordinate)) {
      return false;
    }
    const occupyingUnit = state.units.find(
      (unit) => unit.position.q === coordinate.q && unit.position.r === coordinate.r
    ) ?? null;
    if (occupyingUnit === null) {
      return true;
    }
    if (occupyingUnit.ownerPlayerId === selectedUnit.ownerPlayerId) {
      if (this.isDefenderUnit(selectedUnit) || this.isDefenderUnit(occupyingUnit)) {
        return false;
      }
      return selectedUnit.strength + occupyingUnit.strength <= 3;
    }
    return selectedUnit.strength > occupyingUnit.strength;
  }
  getHighlightKind(state, selectedUnit, coordinate) {
    if (!this.canMoveToCoordinate(state, selectedUnit, coordinate)) {
      if (this.isBlockedEnemyTarget(state, selectedUnit, coordinate)) {
        return coordinate.q === state.objectiveCoordinate.q && coordinate.r === state.objectiveCoordinate.r ? "objective-blocked" : "blocked";
      }
      return "none";
    }
    const occupyingUnit = state.units.find(
      (unit) => unit.position.q === coordinate.q && unit.position.r === coordinate.r
    ) ?? null;
      if (occupyingUnit === null) {
        return coordinate.q === state.objectiveCoordinate.q && coordinate.r === state.objectiveCoordinate.r ? "objective" : "move";
      }
      if (coordinate.q === state.objectiveCoordinate.q && coordinate.r === state.objectiveCoordinate.r) {
        return occupyingUnit.ownerPlayerId === selectedUnit.ownerPlayerId ? "merge" : "objective-attack";
      }
      return occupyingUnit.ownerPlayerId === selectedUnit.ownerPlayerId ? "merge" : "attack";
  }
  getFillColor(isObjective, isRingOne, highlightKind, isSelectedTile, currentPlayerId, dominantZonePlayerId, renderMode) {
    if (isSelectedTile && renderMode === "debug") {
      return this.tilePalette.selectedFill;
    }
    if (isObjective) {
      switch (highlightKind) {
        case "objective":
          return this.tilePalette.objectiveActiveFill;
        case "objective-attack":
          return this.tilePalette.attackFill;
        case "objective-blocked":
          return this.tilePalette.objectiveBlockedFill;
        default:
          if (dominantZonePlayerId === "P1") {
            return this.tilePalette.dominantObjectiveFillP1;
          }
          if (dominantZonePlayerId === "P2") {
            return this.tilePalette.dominantObjectiveFillP2;
          }
          return this.tilePalette.objectiveFill;
      }
    }
    switch (highlightKind) {
      case "blocked":
        return this.tilePalette.blockedFill;
      case "move":
        return currentPlayerId === "P1" ? this.tilePalette.moveFillP1 : this.tilePalette.moveFillP2;
      case "merge":
        return this.tilePalette.mergeFill;
      case "attack":
        return this.tilePalette.attackFill;
      default:
        if (isRingOne) {
          if (dominantZonePlayerId === "P1") {
            return this.tilePalette.dominantRingOneFillP1;
          }
          if (dominantZonePlayerId === "P2") {
            return this.tilePalette.dominantRingOneFillP2;
          }
          return this.tilePalette.ringOneFill;
        }
        return this.tilePalette.neutralFill;
    }
  }
  getDominantZonePlayerId(state) {
    let playerOneStrength = 0;
    let playerTwoStrength = 0;
    for (const unit of state.units) {
      const distance = this.hexDistance(unit.position, state.objectiveCoordinate);
      if (distance > 1) {
        continue;
      }
      if (unit.ownerPlayerId === "P1") {
        playerOneStrength += unit.strength;
      } else if (unit.ownerPlayerId === "P2") {
        playerTwoStrength += unit.strength;
      }
    }
    if (playerOneStrength === playerTwoStrength) {
      return null;
    }
    return playerOneStrength > playerTwoStrength ? "P1" : "P2";
  }
  getSelectedStrokeColor(currentPlayerId, renderMode) {
    if (renderMode === "gameplay") {
      return currentPlayerId === "P1" ? this.tilePalette.moveStrokeP1 : this.tilePalette.moveStrokeP2;
    }
    return this.tilePalette.selectedStroke;
  }
  getSelectableStrokeColor(currentPlayerId, renderMode) {
    if (renderMode === "gameplay") {
      return currentPlayerId === "P1" ? this.tilePalette.selectableStrokeP1 : this.tilePalette.selectableStrokeP2;
    }
    return this.tilePalette.selectedStroke;
  }
  getStrokeColor(isObjective, isRingOne, highlightKind, currentPlayerId, renderMode) {
    if (isObjective) {
      switch (highlightKind) {
        case "objective":
          return this.tilePalette.objectiveActiveStroke;
        case "objective-attack":
          return this.tilePalette.attackStroke;
        case "objective-blocked":
          return this.tilePalette.objectiveStroke;
        default:
          return this.tilePalette.objectiveStroke;
      }
    }
    switch (highlightKind) {
      case "move":
        return currentPlayerId === "P1" ? this.tilePalette.moveStrokeP1 : this.tilePalette.moveStrokeP2;
      default:
        if (renderMode === "gameplay") {
          return isRingOne ? this.tilePalette.gameplayRingOneStroke : this.tilePalette.gameplayNeutralStroke;
        }
        return isRingOne ? this.tilePalette.ringOneStroke : this.tilePalette.neutralStroke;
    }
  }
  hasTraversablePath(state, movingUnit, target) {
    const maxDepth = getMovementDepth(movingUnit);
    if (maxDepth <= 0) {
      return false;
    }
    const visited = /* @__PURE__ */ new Set([this.key(movingUnit.position)]);
    const queue = [
      { coordinate: movingUnit.position, depth: 0 }
    ];
    while (queue.length > 0) {
      const current = queue.shift();
      if (current.depth >= maxDepth) {
        continue;
      }
      for (const neighbor of this.getAdjacentCoordinates(current.coordinate)) {
        const nextDepth = current.depth + 1;
        if (neighbor.q === target.q && neighbor.r === target.r) {
          return true;
        }
        if (!this.canTraverseIntermediateCoordinate(state, movingUnit, neighbor) || visited.has(this.key(neighbor))) {
          continue;
        }
        visited.add(this.key(neighbor));
        queue.push({ coordinate: neighbor, depth: nextDepth });
      }
    }
    return false;
  }
  canTraverseIntermediateCoordinate(state, movingUnit, coordinate) {
    const occupyingUnit = state.units.find(
      (unit) => unit.position.q === coordinate.q && unit.position.r === coordinate.r
    ) ?? null;
    if (occupyingUnit !== null) {
      return false;
    }
    return true;
  }
  isBlockedEnemyTarget(state, movingUnit, coordinate) {
    if (!this.hasTraversablePath(state, movingUnit, coordinate)) {
      return false;
    }
    const occupyingUnit = state.units.find(
      (unit) => unit.position.q === coordinate.q && unit.position.r === coordinate.r
    ) ?? null;
    if (occupyingUnit === null || occupyingUnit.ownerPlayerId === movingUnit.ownerPlayerId) {
      return false;
    }
    return movingUnit.strength <= occupyingUnit.strength;
  }
  getBlockedEdgeIndexes(state, movingUnit, coordinate, center) {
    const occupyingUnit = state.units.find(
      (unit) => unit.position.q === coordinate.q && unit.position.r === coordinate.r
    ) ?? null;
    if (occupyingUnit !== null && occupyingUnit.ownerPlayerId !== movingUnit.ownerPlayerId && movingUnit.strength <= occupyingUnit.strength) {
      const movingUnitCenter = this.hexCenters.get(this.key(movingUnit.position));
      return movingUnitCenter ? [this.getEdgeIndexForNeighbor(center, movingUnitCenter)] : [];
    }
    return state.units.filter(
      (unit) => unit.ownerPlayerId !== movingUnit.ownerPlayerId && (unit.position.q !== coordinate.q || unit.position.r !== coordinate.r) && this.areAdjacent(unit.position, coordinate)
    ).map((unit) => {
      const enemyCenter = this.hexCenters.get(this.key(unit.position));
      return enemyCenter ? this.getEdgeIndexForNeighbor(center, enemyCenter) : null;
    }).filter((edgeIndex) => edgeIndex !== null).filter((edgeIndex, index, all) => all.indexOf(edgeIndex) === index);
  }
  getEdgeIndexForNeighbor(center, neighborCenter) {
    const dx = neighborCenter.x - center.x;
    const dy = neighborCenter.y - center.y;
    const angle = (Math.atan2(dy, dx) * 180 / Math.PI + 360) % 360;
    return Math.round(angle / 60) % 6;
  }
}
const config = window.kingOfTheHillConfig ?? {};
const client = new KingOfTheHillClient(
  (config.backendBaseUrl ?? "http://localhost:5091").replace(/\/$/, ""),
  config.gameDefinitionId ?? "king-of-the-hill"
);
const canvas = document.querySelector("#game-canvas");
const boardArea = document.querySelector(".board-area");
const metaPanel = document.querySelector("#match-meta");
const matchSetupMetaPanel = document.querySelector("#match-setup-meta");
const selectionPanel = document.querySelector("#selection-panel");
const logPanel = document.querySelector("#log-panel");
const turnBanner = document.querySelector("#turn-banner");
const turnBannerPlayer = document.querySelector("#turn-banner-player");
const scoreBanner = document.querySelector("#score-banner");
const scoreBannerP1 = document.querySelector("#score-banner-p1");
const scoreBannerP2 = document.querySelector("#score-banner-p2");
const boardHint = document.querySelector("#board-hint");
const resolutionBanner = document.querySelector("#resolution-banner");
const turnBannerStatus = document.querySelector("#turn-banner-status");
const stopButton = document.querySelector("#stop-button");
const resumeButton = document.querySelector("#resume-button");
const newMatchButton = document.querySelector("#new-match-button");
const passButton = document.querySelector("#pass-button");
const clearSelectionButton = document.querySelector("#clear-selection-button");
const undoStopButton = document.querySelector("#undo-stop-button");
const gameplayViewCheckbox = document.querySelector("#gameplay-view-checkbox");
const aiStatusPanel = document.querySelector("#ai-status-panel");
const aiTelemetryPanel = document.querySelector("#ai-telemetry-panel");
const winnerModal = document.querySelector("#winner-modal");
const winnerTitle = document.querySelector("#winner-title");
const winnerMessage = document.querySelector("#winner-message");
const winnerNewMatchButton = document.querySelector("#winner-new-match-button");
const confirmNewMatchModal = document.querySelector("#confirm-new-match-modal");
const confirmNewMatchAcceptButton = document.querySelector("#confirm-new-match-accept-button");
const confirmNewMatchLoadButton = document.querySelector("#confirm-new-match-load-button");
const confirmNewMatchCancelButton = document.querySelector("#confirm-new-match-cancel-button");
const confirmNewMatchRewindCheckbox = document.querySelector("#confirm-new-match-rewind-checkbox");
const matchSetupModal = document.querySelector("#match-setup-modal");
const player1KindSelect = document.querySelector("#player-1-kind-select");
const player2KindSelect = document.querySelector("#player-2-kind-select");
const matchSetupStartButton = document.querySelector("#match-setup-start-button");
const matchSetupLoadButton = document.querySelector("#match-setup-load-button");
const matchSetupCancelButton = document.querySelector("#match-setup-cancel-button");
const matchSetupRewindCheckbox = document.querySelector("#match-setup-rewind-checkbox");
if (!canvas || !boardArea || !metaPanel || !matchSetupMetaPanel || !selectionPanel || !logPanel || !turnBanner || !turnBannerPlayer || !scoreBanner || !scoreBannerP1 || !scoreBannerP2 || !boardHint || !resolutionBanner || !stopButton || !resumeButton || !newMatchButton || !passButton || !clearSelectionButton || !undoStopButton || !gameplayViewCheckbox || !aiStatusPanel || !aiTelemetryPanel || !winnerModal || !winnerTitle || !winnerMessage || !winnerNewMatchButton || !confirmNewMatchModal || !confirmNewMatchAcceptButton || !confirmNewMatchLoadButton || !confirmNewMatchCancelButton || !confirmNewMatchRewindCheckbox || !matchSetupModal || !player1KindSelect || !player2KindSelect || !matchSetupStartButton || !matchSetupLoadButton || !matchSetupCancelButton || !matchSetupRewindCheckbox) {
  throw new Error("The web tool could not find the required DOM elements.");
}
const savedMatchStorageKey = "hexstrategy.king-of-the-hill.last-match";
clearSelectionButton.disabled = true;
const renderer = new CanvasBoardRenderer(canvas);
let currentMatch = null;
let selectedUnit = null;
let lastRenderedPlayerId = null;
let isAutomatedTurnPending = false;
let isAutomationPaused = false;
let isResolutionPlaybackActive = false;
let automatedTurnHandle = null;
let winnerModalHandle = null;
let winnerModalMatchKey = null;
let renderMode = "gameplay";
let hoveredCoordinate = null;
let transientBoardHint = null;
let transientResolutionText = null;
let transientBoardState = null;
let transientSelectedUnit = null;
let siegeAnimationFrameHandle = null;
let currentSetup = {
  player1: "Human",
  player2: "IA4"
};
player1KindSelect.value = currentSetup.player1;
player2KindSelect.value = currentSetup.player2;
async function startNewMatch(setup) {
  cancelPendingAutomatedTurn();
  clearWinnerModalDelay();
  isAutomationPaused = false;
  clearResolutionPlayback();
  clearSavedMatchRecord();
  currentSetup = setup;
  currentMatch = await client.createMatch(setup);
  selectedUnit = null;
  hoveredCoordinate = null;
  persistCurrentMatch("Autosaved snapshot", true);
  render();
  pushTurnResolution(currentMatch.lastMessage);
  scheduleAutomatedTurnIfNeeded();
}
function render() {
  if (!currentMatch) {
    renderSetupMeta();
    refreshSavedMatchButtons();
    return;
  }
  const boardMatch = transientBoardState === null ? currentMatch : { ...currentMatch, state: transientBoardState };
  const objectiveSiegePulse = 0;
  const effectiveSelectedUnit = transientSelectedUnit ?? selectedUnit;
  renderer.render(boardMatch, effectiveSelectedUnit, renderMode, hoveredCoordinate, objectiveSiegePulse);
  renderMeta(currentMatch);
  renderSelection(currentMatch.state, effectiveSelectedUnit);
  renderTurnBanner(currentMatch);
  renderAiTelemetry(currentMatch);
  const humanTurnLocked = isAutomatedTurnPending || isAutomationPaused || isResolutionPlaybackActive || !isHumanTurn(currentMatch.state);
  const canPauseMatch = !currentMatch.state.isCompleted && !isAutomationPaused;
  const canResumeMatch = !currentMatch.state.isCompleted && isAutomationPaused;
  const savedRecord = getSavedMatchRecord();
  const hasRewindSnapshot = savedRecord?.previousSnapshot !== null && savedRecord?.previousSnapshot !== void 0;
  boardArea.classList.toggle("board-area-paused", isAutomationPaused);
  boardArea.classList.toggle("board-area-resolving", isResolutionPlaybackActive);
  const shouldShowBoardHint = transientBoardHint !== null || isHumanTurn(currentMatch.state) && !isAutomationPaused && !isResolutionPlaybackActive && !currentMatch.state.isCompleted;
  boardHint.classList.toggle("hidden", !shouldShowBoardHint);
  boardHint.classList.toggle("board-hint-resolution", transientBoardHint !== null);
  boardHint.classList.remove("board-hint-siege");
  const isMoveHint = transientBoardHint === null && selectedUnit !== null && !isAutomationPaused && !isResolutionPlaybackActive && !currentMatch.state.isCompleted;
  boardHint.classList.toggle("board-hint-move-p1", isMoveHint && currentMatch.state.currentPlayerId === "P1");
  boardHint.classList.toggle("board-hint-move-p2", isMoveHint && currentMatch.state.currentPlayerId === "P2");
  boardHint.textContent = transientBoardHint ?? getBoardHintText(currentMatch.state, transientSelectedUnit ?? selectedUnit);
  resolutionBanner.classList.toggle("hidden", transientResolutionText === null);
  resolutionBanner.classList.remove("resolution-banner-siege");
  resolutionBanner.textContent = transientResolutionText ?? "";
  const canClearSelection = selectedUnit !== null && !humanTurnLocked;
  clearSelectionButton.classList.toggle("hidden", !canClearSelection);
  clearSelectionButton.classList.toggle("board-clear-selection-p1", canClearSelection && currentMatch.state.currentPlayerId === "P1");
  clearSelectionButton.classList.toggle("board-clear-selection-p2", canClearSelection && currentMatch.state.currentPlayerId === "P2");
  clearSelectionButton.disabled = !canClearSelection;
  passButton.disabled = currentMatch.state.isCompleted || humanTurnLocked;
  stopButton.disabled = !canPauseMatch;
  resumeButton.disabled = !canResumeMatch;
  newMatchButton.disabled = false;
  undoStopButton.textContent = isAutomationPaused ? "Resume" : "Undo & Pause";
  undoStopButton.disabled = isAutomationPaused ? !canResumeMatch : !hasRewindSnapshot;
  refreshSavedMatchButtons();
  renderWinnerModal(currentMatch);
}
function getBoardHintText(state, unit) {
  return unit === null ? "Click one of your units" : "Click an available hex to move";
}
function areAdjacent(left, right) {
  const dq = right.q - left.q;
  const dr = right.r - left.r;
  return (
    (dq === 1 && dr === 0) ||
    (dq === 1 && dr === -1) ||
    (dq === 0 && dr === -1) ||
    (dq === -1 && dr === 0) ||
    (dq === -1 && dr === 1) ||
    (dq === 0 && dr === 1)
  );
}
function renderMeta(match) {
  const state = match.state;
  const p1Units = getPlayerUnits(state, "P1");
  const p2Units = getPlayerUnits(state, "P2");
  const p1Strength = getPlayerTotalStrength(state, "P1");
  const p2Strength = getPlayerTotalStrength(state, "P2");
  metaPanel.innerHTML = "";
  metaPanel.append(
    makeMetaRow("Game", match.gameDefinitionId),
    makeMetaRow("Turn", state.turnNumber.toString()),
    makeMetaRow("Current Player", state.currentPlayer.displayName),
    makeMetaRow("P1 Units", `${p1Units}`),
    makeMetaRow("P1 Total Strength", `S${p1Strength}`),
    makeMetaRow("P2 Units", `${p2Units}`),
    makeMetaRow("P2 Total Strength", `S${p2Strength}`),
    makeMetaRow("Winner", state.winnerPlayerId ?? "-")
  );
  renderScoreBanner(state);
  renderSetupMeta();
}
function renderScoreBanner(state) {
  scoreBannerP1.textContent = formatScoreBannerLine(
    "P1",
    getPlayerUnits(state, "P1"),
    getPlayerTotalStrength(state, "P1")
  );
  scoreBannerP2.textContent = formatScoreBannerLine(
    "P2",
    getPlayerUnits(state, "P2"),
    getPlayerTotalStrength(state, "P2")
  );
}
function formatScoreBannerLine(playerId, unitCount, totalStrength) {
  return `${playerId} U${unitCount} | S${totalStrength}`;
}
function getPlayerUnits(state, playerId) {
  return state.units.filter((unit) => unit.ownerPlayerId === playerId).length;
}
function getPlayerTotalStrength(state, playerId) {
  return state.units
    .filter((unit) => unit.ownerPlayerId === playerId)
    .reduce((total, unit) => total + unit.strength, 0);
}
function renderSetupMeta() {
  matchSetupMetaPanel.innerHTML = "";
  matchSetupMetaPanel.append(
    makeMetaRow("Player 1", currentSetup.player1),
    makeMetaRow("Player 2", currentSetup.player2)
  );
}
function updateMatchSetupCancelVisibility() {
  matchSetupCancelButton.classList.toggle("hidden", currentMatch === null);
}
function renderSelection(state, unit) {
  selectionPanel.innerHTML = "";
  if (!unit) {
    selectionPanel.append(makeMetaRow("Selected Unit", "None"));
    selectionPanel.append(makeMetaRow("Hint", "Select one highlighted unit to start your turn."));
    return;
  }
  selectionPanel.append(
    makeMetaRow("Selected Unit", unit.id),
    makeMetaRow("Owner", unit.ownerPlayerId),
    makeMetaRow("Position", `${unit.position.q}, ${unit.position.r}`),
    makeMetaRow("Move Range", `${getMovementDepth(unit)}`),
    makeMetaRow("Strength", `S${unit.strength}`),
    makeMetaRow("Members", unit.memberUnitIds.join(", "))
  );
}
function getMovementDepth(unit) {
  return unit.strength === 1 ? 2 : 1;
}
function isWithinRawMovementRange(renderer, unit, coordinate) {
  return renderer.getReachableCoordinates(unit.position, getMovementDepth(unit)).some(
    (target) => target.q === coordinate.q && target.r === coordinate.r
  );
}
function makeMetaRow(label, value) {
  const row = document.createElement("div");
  row.className = "meta-row";
  row.innerHTML = `<strong>${label}</strong><span>${value}</span>`;
  return row;
}
function pushLog(message) {
  const entry = document.createElement("div");
  entry.className = "log-entry";
  entry.textContent = message;
  logPanel.prepend(entry);
}
function pushTurnResolution(message) {
  const steps = buildResolutionSteps(message);
  for (const step of steps) {
    pushLog(step.logText);
  }
}
function buildResolutionSteps(message) {
  const sentences = message
    .split(". ")
    .map((sentence) => sentence.trim())
    .filter((sentence) => sentence.length > 0)
    .map((sentence) => sentence.endsWith(".") ? sentence : `${sentence}.`);
  return sentences.map((sentence) => ({ logText: sentence, bannerText: sentence, boardHintText: null, boardState: null }));
}
function clearResolutionPlayback() {
  isResolutionPlaybackActive = false;
  transientBoardHint = null;
  transientResolutionText = null;
  transientBoardState = null;
  transientSelectedUnit = null;
}
function wait(milliseconds) {
  return new Promise((resolve) => {
    window.setTimeout(resolve, milliseconds);
  });
}
function cloneState(state) {
  return {
    ...state,
    board: {
      ...state.board,
      coordinates: state.board.coordinates.map((coordinate) => ({ ...coordinate }))
    },
    players: state.players.map((player) => ({ ...player })),
    units: state.units.map((unit) => ({
      ...unit,
      position: { ...unit.position },
      memberUnitIds: [...unit.memberUnitIds]
    })),
    controlScores: { ...state.controlScores },
    objectiveCoordinate: { ...state.objectiveCoordinate },
    currentPlayer: { ...state.currentPlayer }
  };
}
function buildIntermediateBoardState(previousState, sentence) {
  const movedMatch = sentence.match(/moved ([A-Z0-9~]+) to \((-?\d+),(-?\d+)\)/i);
  if (movedMatch) {
    const [, unitId, qValue, rValue] = movedMatch;
    const state = cloneState(previousState);
    const unit = state.units.find((candidate) => candidate.id === unitId);
    if (!unit) {
      return null;
    }
    unit.position = { q: Number(qValue), r: Number(rValue) };
    return state;
  }
  const mergeMatch = sentence.match(/merged ([A-Z0-9~]+) into ([A-Z0-9~]+) at \((-?\d+),(-?\d+)\) \(S(\d+)\)/i);
  if (mergeMatch) {
    const [, sourceUnitId, targetUnitId, qValue, rValue] = mergeMatch;
    const state = cloneState(previousState);
    const sourceUnit = state.units.find((candidate) => candidate.id === sourceUnitId);
    const targetUnit = state.units.find((candidate) => candidate.id === targetUnitId);
    if (!sourceUnit || !targetUnit) {
      return null;
    }
    const mergedMemberIds = [...new Set([...sourceUnit.memberUnitIds, ...targetUnit.memberUnitIds])].sort((left, right) => left.localeCompare(right));
    state.units = state.units
      .filter((candidate) => candidate.id !== sourceUnitId && candidate.id !== targetUnitId)
      .concat({
        id: targetUnitId,
        ownerPlayerId: targetUnit.ownerPlayerId,
        position: { q: Number(qValue), r: Number(rValue) },
        memberUnitIds: mergedMemberIds,
        strength: mergedMemberIds.length
      })
      .sort((left, right) => left.id.localeCompare(right.id));
    return state;
  }
  const attackMatch = sentence.match(/attacked and eliminated ([A-Z0-9~]+) with ([A-Z0-9~]+) at \((-?\d+),(-?\d+)\)/i);
  if (attackMatch) {
    const [, defenderUnitId, attackerUnitId, qValue, rValue] = attackMatch;
    const state = cloneState(previousState);
    const attacker = state.units.find((candidate) => candidate.id === attackerUnitId);
    if (!attacker) {
      return null;
    }
    state.units = state.units
      .filter((candidate) => candidate.id !== defenderUnitId)
      .map((candidate) => candidate.id === attackerUnitId ? { ...candidate, position: { q: Number(qValue), r: Number(rValue) } } : candidate);
    return state;
  }
  return null;
}
function extractActingUnitId(sentence) {
  if (!sentence) {
    return null;
  }
  const movedMatch = sentence.match(/moved ([A-Z0-9~]+) to \((-?\d+),(-?\d+)\)/i);
  if (movedMatch) {
    return movedMatch[1] ?? null;
  }
  const mergeMatch = sentence.match(/merged ([A-Z0-9~]+) into ([A-Z0-9~]+) at \((-?\d+),(-?\d+)\) \(S(\d+)\)/i);
  if (mergeMatch) {
    return mergeMatch[1] ?? null;
  }
  const attackMatch = sentence.match(/attacked and eliminated ([A-Z0-9~]+) with ([A-Z0-9~]+) at \((-?\d+),(-?\d+)\)/i);
  if (attackMatch) {
    return attackMatch[2] ?? null;
  }
  return null;
}
function buildResolutionPlaybackSteps(previousState, finalState, message) {
  const steps = buildResolutionSteps(message);
  const actionSentence = message
    .split(". ")
    .map((sentence) => sentence.trim())
    .filter((sentence) => sentence.length > 0)
    .map((sentence) => sentence.endsWith(".") ? sentence : `${sentence}.`)[0] ?? null;
  const intermediateState = actionSentence === null ? null : buildIntermediateBoardState(previousState, actionSentence);
  return steps.map((step, index) => {
    if (index === 0) {
      return {
        ...step,
        boardState: intermediateState
      };
    }
    return {
      ...step,
      boardState: finalState
    };
  });
}
async function presentTurnResolution(previousState, finalState, message) {
  const steps = buildResolutionPlaybackSteps(previousState, finalState, message);
  const actionSentence = message
    .split(". ")
    .map((sentence) => sentence.trim())
    .filter((sentence) => sentence.length > 0)
    .map((sentence) => sentence.endsWith(".") ? sentence : `${sentence}.`)[0] ?? null;
  const actingUnitId = previousState.currentPlayer.kind === "Human" ? null : extractActingUnitId(actionSentence);
  const actingUnit = actingUnitId === null ? null : previousState.units.find((unit) => unit.id === actingUnitId) ?? null;
  const requiresPlayback = steps.length > 1;
  if (!requiresPlayback && actingUnit === null) {
    clearResolutionPlayback();
    render();
    pushTurnResolution(message);
    return;
  }
  isResolutionPlaybackActive = true;
  if (actingUnit !== null) {
    transientBoardState = previousState;
    transientSelectedUnit = { ...actingUnit, position: { ...actingUnit.position }, memberUnitIds: [...actingUnit.memberUnitIds] };
    transientBoardHint = null;
    transientResolutionText = `${previousState.currentPlayer.displayName} selects ${actingUnit.id}.`;
    render();
    await wait(1e3);
    transientSelectedUnit = null;
  }
  for (let index = 0; index < steps.length; index += 1) {
    const step = steps[index];
    transientBoardHint = step.boardHintText;
    transientBoardState = step.boardState;
    transientResolutionText = index === 0 ? `Step 1: ${step.bannerText}` : step.bannerText;
    render();
    pushLog(step.logText);
    if (index < steps.length - 1) {
      await wait(index === 0 && actingUnit !== null ? 1e3 : index === 0 ? 2e3 : 950);
    }
  }
  await wait(actingUnit !== null ? 1e3 : 700);
  clearResolutionPlayback();
  render();
}
function formatSavedAt(isoValue) {
  return new Date(isoValue).toLocaleString();
}
function renderTurnBanner(match) {
  const currentPlayer = match.state.currentPlayer;
  turnBannerPlayer.textContent = currentPlayer.id;
  turnBanner.classList.toggle("turn-banner-p1", currentPlayer.id === "P1");
  turnBanner.classList.toggle("turn-banner-p2", currentPlayer.id === "P2");
  applyActiveTurnButtonAccent(currentPlayer.id);
  if (lastRenderedPlayerId !== null && lastRenderedPlayerId !== currentPlayer.id) {
    turnBanner.classList.remove("turn-banner-animate");
    void turnBanner.offsetWidth;
    turnBanner.classList.add("turn-banner-animate");
    window.setTimeout(() => turnBanner.classList.remove("turn-banner-animate"), 320);
  }
  lastRenderedPlayerId = currentPlayer.id;
}
function applyActiveTurnButtonAccent(playerId) {
  const accentButtons = [passButton];
  const isPlayerOne = playerId === "P1";
  for (const button of accentButtons) {
    button.classList.toggle("toolbar-button-p1", isPlayerOne);
    button.classList.toggle("toolbar-button-p2", !isPlayerOne);
  }
}
function renderAiTelemetry(match) {
  aiTelemetryPanel.innerHTML = "";
  if (isAutomatedTurnPending) {
    aiStatusPanel.textContent = `${match.state.currentPlayer.displayName} (${formatControllerType(match.state.currentPlayer.controllerType)}) will act in 2 seconds.`;
  } else if (isAutomationPaused) {
    aiStatusPanel.textContent = "Match paused. Click Resume to continue.";
  } else if (match.lastAutomatedDecisionTelemetry) {
    aiStatusPanel.textContent = `${match.lastAutomatedDecisionTelemetry.playerDisplayName} completed an automated turn.`;
  } else if (!isHumanTurn(match.state) && !match.state.isCompleted) {
    aiStatusPanel.textContent = `${match.state.currentPlayer.displayName} is automated and waiting for the evaluation delay.`;
  } else {
    aiStatusPanel.textContent = "No automated turn has been executed yet.";
  }
  const telemetry = match.lastAutomatedDecisionTelemetry;
  if (!telemetry) {
    aiTelemetryPanel.append(
      makeMetaRow("Last AI", "-"),
      makeMetaRow("Command", "-"),
      makeMetaRow("Elapsed", "-"),
      makeMetaRow("Nodes", "-")
    );
    return;
  }
  aiTelemetryPanel.append(
    makeMetaRow("Last AI", `${telemetry.playerDisplayName} (${formatControllerType(telemetry.controllerType)})`),
    makeMetaRow("Rule", `${telemetry.decisionRuleCode} ${telemetry.decisionRuleName}`),
    ...telemetry.decisionDiagnostics ? [makeMetaRow("Compare", telemetry.decisionDiagnostics)] : [],
    makeMetaRow("Command", telemetry.chosenCommandDescription),
    makeMetaRow("Elapsed", `${telemetry.elapsedMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Generate", `${telemetry.generationMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Preview", `${telemetry.previewMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Preview Engine", `${telemetry.previewExecutionMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Preview Eval", `${telemetry.previewBaseEvaluationMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Preview Bias", `${telemetry.previewImmediateBiasMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Select", `${telemetry.selectionMilliseconds.toFixed(0)} ms`),
    makeMetaRow("Budget", `${telemetry.timeBudgetMilliseconds} ms`),
    makeMetaRow("Cutoff", telemetry.timeBudgetReached ? "Yes" : "No"),
    makeMetaRow("Depth", telemetry.searchDepth.toString()),
    makeMetaRow("Legal Moves", telemetry.legalCommandCount.toString()),
    makeMetaRow("Candidates", telemetry.candidateCommandCount.toString()),
    makeMetaRow("Nodes", telemetry.nodesVisited.toString()),
    makeMetaRow("Leaves", telemetry.leafEvaluations.toString()),
    makeMetaRow("Score", telemetry.chosenCommandScore.toString())
  );
}
function isHumanTurn(state) {
  return state.currentPlayer.kind === "Human";
}
function formatControllerType(controllerType) {
  switch (controllerType) {
    case "IaLevel1":
    case "IaLevel2":
    case "IaLevel3":
    case "IaLevel4":
      return "IA4";
    default:
      return controllerType;
  }
}
function normalizePlayerProfile(profile) {
  return profile === "Human" ? "Human" : "IA4";
}
function normalizeMatchSetup(setup) {
  return {
    player1: normalizePlayerProfile(setup?.player1),
    player2: normalizePlayerProfile(setup?.player2)
  };
}
function getSavedMatchRecord() {
  try {
    const raw = window.localStorage.getItem(savedMatchStorageKey);
    if (!raw) {
      return null;
    }
    const record = JSON.parse(raw);
    return {
      ...record,
      setup: normalizeMatchSetup(record.setup)
    };
  } catch {
    return null;
  }
}
function getRewindRequested() {
  return confirmNewMatchRewindCheckbox.checked || matchSetupRewindCheckbox.checked;
}
function setRewindRequested(value) {
  confirmNewMatchRewindCheckbox.checked = value;
  matchSetupRewindCheckbox.checked = value;
}
function persistCurrentMatch(logPrefix, resetHistory = false) {
  if (!currentMatch) {
    return;
  }
  const existingRecord = getSavedMatchRecord();
  const latestSnapshot = {
    state: currentMatch.state,
    lastMessage: currentMatch.lastMessage,
    savedAt: (/* @__PURE__ */ new Date()).toISOString()
  };
  const record = {
    latestSnapshot,
    previousSnapshot: resetHistory ? null : existingRecord?.latestSnapshot ?? null,
    gameDefinitionId: currentMatch.gameDefinitionId,
    setup: currentSetup
  };
  window.localStorage.setItem(savedMatchStorageKey, JSON.stringify(record));
  refreshSavedMatchButtons();
  if (logPrefix && !stringEqualsIgnoreCase(logPrefix, "Autosaved snapshot")) {
    pushLog(`${logPrefix} at ${formatSavedAt(latestSnapshot.savedAt)}.`);
  }
}
function clearSavedMatchRecord() {
  window.localStorage.removeItem(savedMatchStorageKey);
  refreshSavedMatchButtons();
}
function refreshSavedMatchButtons() {
  const record = getSavedMatchRecord();
  const wantsRewind = getRewindRequested();
  const hasTargetSnapshot = wantsRewind ? record?.previousSnapshot !== null && record?.previousSnapshot !== void 0 : record?.latestSnapshot !== void 0;
  confirmNewMatchLoadButton.disabled = !hasTargetSnapshot;
  matchSetupLoadButton.disabled = !hasTargetSnapshot;
}
async function loadLastSavedMatch() {
  const record = getSavedMatchRecord();
  if (!record) {
    pushLog("No saved match was found in local storage.");
    refreshSavedMatchButtons();
    return;
  }
  const wantsRewind = getRewindRequested();
  const snapshot = wantsRewind ? record.previousSnapshot : record.latestSnapshot;
  if (!snapshot) {
    pushLog("No rewind snapshot is available yet.");
    refreshSavedMatchButtons();
    return;
  }
  try {
    cancelPendingAutomatedTurn();
    clearWinnerModalDelay();
    isAutomationPaused = false;
    currentSetup = normalizeMatchSetup(record.setup);
    player1KindSelect.value = currentSetup.player1;
    player2KindSelect.value = currentSetup.player2;
    currentMatch = await client.importMatch(snapshot.state, snapshot.lastMessage);
    selectedUnit = null;
    hideModal(confirmNewMatchModal);
    hideModal(matchSetupModal);
    hideModal(winnerModal);
    persistCurrentMatch("Autosaved snapshot", true);
    render();
    pushLog(`${wantsRewind ? "Loaded rewound snapshot" : "Loaded saved match"} from ${formatSavedAt(snapshot.savedAt)}.`);
    scheduleAutomatedTurnIfNeeded();
  } catch (error) {
    clearSavedMatchRecord();
    pushLog(`Saved match could not be loaded: ${error.message}`);
  }
}
async function loadSnapshot(snapshot, setup, options) {
  cancelPendingAutomatedTurn();
  clearWinnerModalDelay();
  isAutomationPaused = false;
  currentSetup = setup;
  player1KindSelect.value = currentSetup.player1;
  player2KindSelect.value = currentSetup.player2;
  currentMatch = await client.importMatch(snapshot.state, snapshot.lastMessage);
  selectedUnit = null;
  hideModal(confirmNewMatchModal);
  hideModal(matchSetupModal);
  hideModal(winnerModal);
  if (options?.pauseAfterLoad) {
    isAutomationPaused = true;
  }
  persistCurrentMatch("Autosaved snapshot", options?.resetHistory ?? true);
  render();
  if (options?.successMessage) {
    pushLog(options.successMessage);
  }
  scheduleAutomatedTurnIfNeeded();
}
async function undoAndStop() {
  const record = getSavedMatchRecord();
  const snapshot = record?.previousSnapshot ?? null;
  if (!record || !snapshot) {
    pushLog("No rewind snapshot is available for Undo & Pause.");
    refreshSavedMatchButtons();
    render();
    return;
  }
  try {
    await loadSnapshot(snapshot, record.setup, {
      pauseAfterLoad: true,
      resetHistory: true,
      successMessage: `Rewound one move and paused for inspection from ${formatSavedAt(snapshot.savedAt)}.`
    });
  } catch (error) {
    clearSavedMatchRecord();
    pushLog(`Undo & Pause failed: ${error.message}`);
  }
}
function cancelPendingAutomatedTurn() {
  if (automatedTurnHandle !== null) {
    window.clearTimeout(automatedTurnHandle);
    automatedTurnHandle = null;
  }
  isAutomatedTurnPending = false;
}
function scheduleAutomatedTurnIfNeeded() {
  if (!currentMatch || currentMatch.state.isCompleted || isHumanTurn(currentMatch.state) || isAutomationPaused || isResolutionPlaybackActive) {
    cancelPendingAutomatedTurn();
    render();
    return;
  }
  if (automatedTurnHandle !== null) {
    return;
  }
  isAutomatedTurnPending = true;
  render();
  automatedTurnHandle = window.setTimeout(async () => {
    automatedTurnHandle = null;
    if (!currentMatch || currentMatch.state.isCompleted || isHumanTurn(currentMatch.state) || isAutomationPaused || isResolutionPlaybackActive) {
      isAutomatedTurnPending = false;
      render();
      return;
    }
    try {
      const previousState = cloneState(currentMatch.state);
      currentMatch = await client.executeAutomatedTurn(currentMatch.matchId);
      selectedUnit = null;
      persistCurrentMatch("Autosaved snapshot");
      isAutomatedTurnPending = false;
      pushAutomatedDecisionLog(currentMatch.lastAutomatedDecisionTelemetry);
      render();
      await presentTurnResolution(previousState, currentMatch.state, currentMatch.lastMessage);
      scheduleAutomatedTurnIfNeeded();
    } catch (error) {
      isAutomatedTurnPending = false;
      render();
      pushLog(error.message);
    }
  }, 2e3);
}
function showModal(modal) {
  if (modal === matchSetupModal) {
    updateMatchSetupCancelVisibility();
  }
  modal.classList.remove("hidden");
  modal.setAttribute("aria-hidden", "false");
}
function hideModal(modal) {
  modal.classList.add("hidden");
  modal.setAttribute("aria-hidden", "true");
}
function pushAutomatedDecisionLog(telemetry) {
  if (!telemetry) {
    return;
  }
  pushLog(
    `${telemetry.playerDisplayName} (${formatControllerType(telemetry.controllerType)}) ` +
    `[${telemetry.decisionRuleCode}] ${telemetry.decisionRuleName} -> ${telemetry.chosenCommandDescription}`
  );
  if (telemetry.decisionDiagnostics) {
    pushLog(`Rule compare: ${telemetry.decisionDiagnostics}`);
  }
}
function stringEqualsIgnoreCase(left, right) {
  return left.localeCompare(right, void 0, { sensitivity: "accent" }) === 0;
}
function clearWinnerModalDelay() {
  if (winnerModalHandle !== null) {
    window.clearTimeout(winnerModalHandle);
    winnerModalHandle = null;
  }
  winnerModalMatchKey = null;
}
function renderWinnerModal(match) {
  if (!match.state.isCompleted || !match.state.winnerPlayerId) {
    clearWinnerModalDelay();
    hideModal(winnerModal);
    return;
  }
  if (isResolutionPlaybackActive) {
    hideModal(winnerModal);
    return;
  }
  const winner = match.state.players.find((player) => player.id === match.state.winnerPlayerId);
  const winnerName = winner?.displayName ?? match.state.winnerPlayerId;
  const modalKey = `${match.matchId}:${match.state.turnNumber}:${match.state.winnerPlayerId}:${match.state.controlScores.P1 ?? 0}:${match.state.controlScores.P2 ?? 0}`;
  winnerTitle.textContent = `${winnerName} wins`;
  winnerMessage.textContent = `${winnerName} captures the Hill and wins.`;
  if (!winnerModal.classList.contains("hidden") && winnerModalMatchKey === modalKey) {
    return;
  }
  if (winnerModalHandle !== null && winnerModalMatchKey === modalKey) {
    return;
  }
  clearWinnerModalDelay();
  hideModal(winnerModal);
  winnerModalMatchKey = modalKey;
  winnerModalHandle = window.setTimeout(() => {
    winnerModalHandle = null;
    if (currentMatch && currentMatch.matchId === match.matchId && currentMatch.state.isCompleted && currentMatch.state.winnerPlayerId === match.state.winnerPlayerId && !isResolutionPlaybackActive) {
      showModal(winnerModal);
    } else {
      winnerModalMatchKey = null;
    }
  }, 1100);
}
function clearSelection(pushMessage = false) {
  if (!selectedUnit) {
    return;
  }
  selectedUnit = null;
  hoveredCoordinate = null;
  render();
  if (pushMessage) {
    pushLog("Selection cleared.");
  }
}
canvas.addEventListener("mousemove", (event) => {
  if (!currentMatch || currentMatch.state.isCompleted || isAutomationPaused || isAutomatedTurnPending || isResolutionPlaybackActive || !selectedUnit) {
    if (hoveredCoordinate !== null) {
      hoveredCoordinate = null;
      render();
    }
    return;
  }
  const bounds = canvas.getBoundingClientRect();
  const scaleX = canvas.width / bounds.width;
  const scaleY = canvas.height / bounds.height;
  const x = (event.clientX - bounds.left) * scaleX;
  const y = (event.clientY - bounds.top) * scaleY;
  const coordinate = renderer.tryGetCoordinateAt(x, y);
  if ((coordinate == null ? void 0 : coordinate.q) === (hoveredCoordinate == null ? void 0 : hoveredCoordinate.q) && (coordinate == null ? void 0 : coordinate.r) === (hoveredCoordinate == null ? void 0 : hoveredCoordinate.r)) {
    return;
  }
  hoveredCoordinate = coordinate;
  render();
});
canvas.addEventListener("mouseleave", () => {
  if (hoveredCoordinate === null) {
    return;
  }
  hoveredCoordinate = null;
  render();
});
canvas.addEventListener("click", async (event) => {
  if (!currentMatch || currentMatch.state.isCompleted || isAutomatedTurnPending || isAutomationPaused || isResolutionPlaybackActive || !isHumanTurn(currentMatch.state)) {
    return;
  }
  const bounds = canvas.getBoundingClientRect();
  const scaleX = canvas.width / bounds.width;
  const scaleY = canvas.height / bounds.height;
  const x = (event.clientX - bounds.left) * scaleX;
  const y = (event.clientY - bounds.top) * scaleY;
  const coordinate = renderer.tryGetCoordinateAt(x, y);
  if (!coordinate) {
    clearSelection(true);
    return;
  }
  const clickedUnit = currentMatch.state.units.find((unit) => unit.position.q === coordinate.q && unit.position.r === coordinate.r);
  if (clickedUnit && clickedUnit.ownerPlayerId === currentMatch.state.currentPlayerId) {
    if ((selectedUnit == null ? void 0 : selectedUnit.id) === clickedUnit.id) {
      clearSelection(true);
      return;
    }
    if (selectedUnit && selectedUnit.id !== clickedUnit.id && renderer.canMoveToCoordinate(currentMatch.state, selectedUnit, clickedUnit.position)) {
      try {
        const previousState = cloneState(currentMatch.state);
        currentMatch = await client.sendCommand(currentMatch.matchId, "move", {
          unitId: selectedUnit.id,
          q: coordinate.q.toString(),
          r: coordinate.r.toString()
        });
        selectedUnit = null;
        hoveredCoordinate = null;
        persistCurrentMatch("Autosaved snapshot");
        render();
        await presentTurnResolution(previousState, currentMatch.state, currentMatch.lastMessage);
        scheduleAutomatedTurnIfNeeded();
      } catch (error) {
        pushLog(error.message);
        renderSelection(currentMatch.state, selectedUnit);
      }
      return;
    }
    selectedUnit = clickedUnit;
    hoveredCoordinate = null;
    render();
    return;
  }
  if (!selectedUnit) {
    pushLog("First select one of your units.");
    return;
  }
  if (!renderer.canMoveToCoordinate(currentMatch.state, selectedUnit, coordinate)) {
    clearSelection(true);
    return;
  }
  try {
    const previousState = cloneState(currentMatch.state);
    currentMatch = await client.sendCommand(currentMatch.matchId, "move", {
      unitId: selectedUnit.id,
      q: coordinate.q.toString(),
      r: coordinate.r.toString()
    });
    selectedUnit = null;
    hoveredCoordinate = null;
    persistCurrentMatch("Autosaved snapshot");
    render();
    await presentTurnResolution(previousState, currentMatch.state, currentMatch.lastMessage);
    scheduleAutomatedTurnIfNeeded();
  } catch (error) {
    pushLog(error.message);
  }
});
newMatchButton.addEventListener("click", async () => {
  if (currentMatch) {
    showModal(confirmNewMatchModal);
    return;
  }
  showModal(matchSetupModal);
});
passButton.addEventListener("click", async () => {
  if (!currentMatch || currentMatch.state.isCompleted || isAutomatedTurnPending || isAutomationPaused || isResolutionPlaybackActive || !isHumanTurn(currentMatch.state)) {
    return;
  }
  try {
    const previousState = cloneState(currentMatch.state);
    currentMatch = await client.sendCommand(currentMatch.matchId, "pass");
    selectedUnit = null;
    persistCurrentMatch("Autosaved snapshot");
    render();
    await presentTurnResolution(previousState, currentMatch.state, currentMatch.lastMessage);
    scheduleAutomatedTurnIfNeeded();
  } catch (error) {
    pushLog(error.message);
  }
});
stopButton.addEventListener("click", () => {
  if (!currentMatch || currentMatch.state.isCompleted || isAutomationPaused) {
    return;
  }
  cancelPendingAutomatedTurn();
  isAutomationPaused = true;
  selectedUnit = null;
  render();
  pushLog("Match paused.");
});
resumeButton.addEventListener("click", () => {
  if (!currentMatch || currentMatch.state.isCompleted || !isAutomationPaused) {
    return;
  }
  isAutomationPaused = false;
  render();
  pushLog("Match resumed.");
  scheduleAutomatedTurnIfNeeded();
});
clearSelectionButton.addEventListener("click", () => {
  if (isAutomationPaused) {
    return;
  }

  clearSelection(true);
});
undoStopButton.addEventListener("click", async () => {
  if (isAutomationPaused) {
    isAutomationPaused = false;
    render();
    pushLog("Match resumed.");
    scheduleAutomatedTurnIfNeeded();
    return;
  }
  await undoAndStop();
});
gameplayViewCheckbox.addEventListener("change", () => {
  renderMode = gameplayViewCheckbox.checked ? "gameplay" : "debug";
  render();
});
winnerNewMatchButton.addEventListener("click", async () => {
  hideModal(winnerModal);
  player1KindSelect.value = currentSetup.player1;
  player2KindSelect.value = currentSetup.player2;
  showModal(matchSetupModal);
});
confirmNewMatchAcceptButton.addEventListener("click", async () => {
  hideModal(confirmNewMatchModal);
  hideModal(winnerModal);
  player1KindSelect.value = currentSetup.player1;
  player2KindSelect.value = currentSetup.player2;
  showModal(matchSetupModal);
});
confirmNewMatchLoadButton.addEventListener("click", async () => {
  await loadLastSavedMatch();
});
confirmNewMatchCancelButton.addEventListener("click", () => {
  hideModal(confirmNewMatchModal);
});
confirmNewMatchRewindCheckbox.addEventListener("change", () => {
  setRewindRequested(confirmNewMatchRewindCheckbox.checked);
  refreshSavedMatchButtons();
  render();
});
matchSetupRewindCheckbox.addEventListener("change", () => {
  setRewindRequested(matchSetupRewindCheckbox.checked);
  refreshSavedMatchButtons();
  render();
});
matchSetupStartButton.addEventListener("click", async () => {
  const setup = {
    player1: player1KindSelect.value,
    player2: player2KindSelect.value
  };
  hideModal(matchSetupModal);
  await startNewMatch(setup);
});
matchSetupLoadButton.addEventListener("click", async () => {
  await loadLastSavedMatch();
});
matchSetupCancelButton.addEventListener("click", () => {
  if (!currentMatch) {
    return;
  }
  hideModal(matchSetupModal);
});
showModal(matchSetupModal);
setRewindRequested(false);
renderSetupMeta();
refreshSavedMatchButtons();
