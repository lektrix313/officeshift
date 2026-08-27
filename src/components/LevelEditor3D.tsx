import { useCallback, useRef, useState } from 'react';
import { Canvas } from '@react-three/fiber';
import { OrbitControls, Grid, Text } from '@react-three/drei';
import * as THREE from 'three';

// ── Types mirrored from LevelDesigner ──
type ElementType =
  | 'wall' | 'glass-wall' | 'glass-partition' | 'door' | 'keycard-door' | 'window' | 'column'
  | 'office' | 'cubicle' | 'reception' | 'meeting-room' | 'server-room' | 'break-room'
  | 'bathroom' | 'storage-closet' | 'executive-office'
  | 'desk' | 'terminal-desk' | 'chair' | 'printer' | 'meeting-table' | 'whiteboard'
  | 'bookshelf' | 'filing-cabinet' | 'water-cooler' | 'coffee-machine' | 'vending-machine'
  | 'sofa' | 'lounge-chair' | 'coffee-table' | 'server-rack' | 'safe' | 'shredder'
  | 'scanner' | 'monitor' | 'projector' | 'tv-screen'
  | 'stair' | 'elevator'
  | 'air-duct' | 'duct-vent' | 'vent-access' | 'hiding-nook' | 'body-disposal'
  | 'plant' | 'prop' | 'clock' | 'fire-extinguisher' | 'coat-rack' | 'umbrella-stand';

interface Vec2 { x: number; y: number; }

interface LevelElement {
  id: string;
  type: ElementType;
  label: string;
  floorId: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: 0 | 90 | 180 | 270;
  room: string;
  accessCardId: string | null;
  gameplay: boolean;
  placeholder: boolean;
}

interface PaletteEntry {
  type: ElementType;
  label: string;
  category: string;
  size: [number, number];
  color: string;
  gameplay: boolean;
}

interface Props {
  elements: LevelElement[];
  activeTool: ElementType;
  palette: PaletteEntry[];
  gridWidth: number;
  gridHeight: number;
  onPlace: (type: ElementType, x: number, y: number, w: number, h: number) => void;
  onSelect: (id: string | null) => void;
  selectedId: string | null;
}

const GRID_SCALE = 2; // 1 grid unit = 2 world units

// ── Color mapping for3D rendering ──
const ELEMENT_COLORS: Record<string, string> = {
  stone: '#89948e', glass: '#79b9cc', door: '#dbad78', 'keycard-door': '#dbad78',
  window: '#a0d8e8', room: '#bf9fb1', cubicle: '#99aeb3', reception: '#ccae77',
  meeting: '#aaa0be', server: '#506470', break: '#b4c8a0', bathroom: '#8cb4d0',
  storage: '#a89888', executive: '#a082aa', desk: '#d5cec0', terminal: '#56aea6',
  chair: '#b9aaa5', printer: '#aebbc0', 'meeting-table': '#c4b89a', whiteboard: '#e8e8e0',
  bookshelf: '#b8a88a', filing: '#a0a8b0', sofa: '#707888', lounge: '#b0a8a0',
  'coffee-table': '#c0b8a0', 'server-rack': '#3c5064', safe: '#888888', shredder: '#909090',
  scanner: '#a0a8b0', monitor: '#283c50', projector: '#646478', tv: '#1e1e28',
  stairs: '#b6afd0', elevator: '#aebdca', plant: '#85a86f', prop: '#d2b262',
  clock: '#c8c8c8', 'fire-ext': '#c83c3c', 'coat-rack': '#a09080', umbrella: '#909098',
  duct: '#786e64', vent: '#64646e', 'vent-access': '#505a64', nook: '#463c32',
  disposal: '#8c3232',
};

function colorFor(type: ElementType, palette: PaletteEntry[]): string {
  const item = palette.find(p => p.type === type);
  return ELEMENT_COLORS[item?.color ?? 'stone'] ?? '#888888';
}

function sizeFor(type: ElementType, palette: PaletteEntry[]): [number, number] {
  const item = palette.find(p => p.type === type);
  return item?.size ?? [2, 2];
}



function heightFor(type: ElementType): number {
  if (['wall', 'glass-wall', 'glass-partition'].includes(type)) return 3;
  if (['office', 'cubicle', 'reception', 'meeting-room', 'server-room', 'break-room',
    'bathroom', 'storage-closet', 'executive-office'].includes(type)) return 0.15;
  if (type === 'keycard-door' || type === 'door') return 2.4;
  if (type === 'desk' || type === 'terminal-desk' || type === 'meeting-table') return 0.75;
  if (type === 'chair' || type === 'lounge-chair') return 0.9;
  if (type === 'server-rack') return 1.8;
  if (type === 'safe') return 1.0;
  if (type === 'bookshelf') return 1.6;
  if (type === 'sofa') return 0.7;
  if (type === 'printer') return 0.7;
  if (type === 'vending-machine') return 1.8;
  if (type === 'coffee-machine') return 0.9;
  if (type === 'water-cooler') return 1.2;
  if (type === 'air-duct') return 0.25;
  if (type === 'duct-vent' || type === 'vent-access') return 0.3;
  if (type === 'hiding-nook' || type === 'body-disposal') return 0.6;
  return 0.6;
}

// ── Placement validation ──
function canPlace(
  type: ElementType, gx: number, gy: number, gw: number, gh: number,
  existing: LevelElement[], gridW: number, gridH: number,
): boolean {
  // Bounds check
  if (gx < 0 || gy < 0 || gx + gw > gridW || gy + gh > gridH) return false;

  // Overlap check (skip for decorations and ducts)
  if (['plant', 'prop', 'clock', 'fire-extinguisher', 'coat-rack', 'umbrella-stand', 'air-duct'].includes(type)) return true;

  for (const el of existing) {
    if (el.id === '') continue; // skip placeholder
    const overlap =
      gx < el.x + el.width && gx + gw > el.x &&
      gy < el.y + el.height && gy + gh > el.y;
    if (overlap) return false;
  }
  return true;
}

// ── Snap grid position from world coords ──


// ── Ghost Preview Component ──
function GhostElement({
  type, gx, gy, gw, gh, valid, palette,
}: {
  type: ElementType; gx: number; gy: number; gw: number; gh: number;
  valid: boolean; palette: PaletteEntry[];
}) {
  const color = valid ? colorFor(type, palette) : '#ff3333';
  const opacity = valid ? 0.5 : 0.6;
  const h = heightFor(type);
  const wx = (gx - gw / 2 + gw / 2) * GRID_SCALE;
  const wz = (gy - gh / 2 + gh / 2) * GRID_SCALE;
  const worldW = gw * GRID_SCALE;
  const worldD = gh * GRID_SCALE;

  return (
    <mesh position={[wx, h / 2, wz]} castShadow>
      <boxGeometry args={[worldW, Math.max(h, 0.15), worldD]} />
      <meshStandardMaterial
        color={color}
        transparent
        opacity={opacity}
        depthWrite={false}
      />
    </mesh>
  );
}

// ── Existing Element 3D Mesh ──
function ElementMesh({ element, palette, isSelected }: {
  element: LevelElement; palette: PaletteEntry[]; isSelected: boolean;
}) {
  const color = colorFor(element.type, palette);
  const h = heightFor(element.type);
  const wx = (element.x - element.width / 2 + element.width / 2) * GRID_SCALE;
  const wz = (element.y - element.height / 2 + element.height / 2) * GRID_SCALE;
  const worldW = element.width * GRID_SCALE;
  const worldD = element.height * GRID_SCALE;
  const isDuct = element.type === 'air-duct';
  const isRoom = ['office', 'cubicle', 'reception', 'meeting-room', 'server-room',
    'break-room', 'bathroom', 'storage-closet', 'executive-office'].includes(element.type);

  return (
    <group>
      <mesh position={[wx, h / 2, wz]} castShadow receiveShadow>
        {isDuct ? (
          <cylinderGeometry args={[0.4, 0.4, worldW, 8]} />
        ) : (
          <boxGeometry args={[worldW, Math.max(h, 0.05), worldD]} />
        )}
        <meshStandardMaterial
          color={color}
          transparent={isRoom || ['glass-wall', 'glass-partition', 'window'].includes(element.type)}
          opacity={
            isRoom ? 0.15 :
            ['glass-wall', 'glass-partition', 'window'].includes(element.type) ? 0.3 :
            0.85
          }
          wireframe={isRoom}
        />
      </mesh>
      {/* Selection highlight */}
      {isSelected && (
        <mesh position={[wx, h / 2, wz]}>
          <boxGeometry args={[worldW + 0.2, Math.max(h, 0.15) + 0.1, worldD + 0.2]} />
          <meshStandardMaterial color="#ffaa33" transparent opacity={0.25} depthWrite={false} wireframe />
        </mesh>
      )}
      {/* Label */}
      {h > 0.5 && (
        <Text
          position={[wx, h + 0.3, wz]}
          fontSize={0.3}
          color="#334"
          anchorX="center"
          anchorY="bottom"
          maxWidth={worldW}
        >
          {element.label}
        </Text>
      )}
    </group>
  );
}

// ── Floor Grid ──
function FloorPlane({ gw, gh }: { gw: number; gh: number }) {
  const size = Math.max(gw, gh) * GRID_SCALE + 4;
  return (
    <group>
      {/* Ground plane */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.05, 0]} receiveShadow>
        <planeGeometry args={[size, size]} />
        <meshStandardMaterial color="#e8e4dc" />
      </mesh>
      {/* Grid lines */}
      <Grid
        args={[gw * GRID_SCALE, gh * GRID_SCALE]}
        cellSize={GRID_SCALE}
        cellThickness={0.5}
        cellColor="#8a9a90"
        sectionSize={GRID_SCALE * 5}
        sectionThickness={1}
        sectionColor="#6a7a70"
        fadeDistance={80}
        fadeStrength={1}
        followCamera={false}
        infiniteGrid={false}
      />
      {/* Border walls */}
      <mesh position={[0, 1.5, -gh * GRID_SCALE / 2]}>
        <boxGeometry args={[gw * GRID_SCALE, 3, 0.2]} />
        <meshStandardMaterial color="#c8c4bc" />
      </mesh>
      <mesh position={[0, 1.5, gh * GRID_SCALE / 2]}>
        <boxGeometry args={[gw * GRID_SCALE, 3, 0.2]} />
        <meshStandardMaterial color="#c8c4bc" />
      </mesh>
      <mesh position={[-gw * GRID_SCALE / 2, 1.5, 0]}>
        <boxGeometry args={[0.2, 3, gh * GRID_SCALE]} />
        <meshStandardMaterial color="#c8c4bc" />
      </mesh>
      <mesh position={[gw * GRID_SCALE / 2, 1.5, 0]}>
        <boxGeometry args={[0.2, 3, gh * GRID_SCALE]} />
        <meshStandardMaterial color="#c8c4bc" />
      </mesh>
    </group>
  );
}

// ── Duct Overlay ──
function DuctOverlay({ elements }: { elements: LevelElement[] }) {
  const ducts = elements.filter(e => e.type === 'air-duct');
  const vents = elements.filter(e => e.type === 'duct-vent' || e.type === 'vent-access');

  if (ducts.length === 0 && vents.length === 0) return null;

  // Draw duct connections between vents
  const lines: [THREE.Vector3, THREE.Vector3][] = [];
  for (let i = 0; i < vents.length; i++) {
    for (let j = i + 1; j < vents.length; j++) {
      const a = vents[i];
      const b = vents[j];
      const ax = a.x * GRID_SCALE;
      const az = a.y * GRID_SCALE;
      const bx = b.x * GRID_SCALE;
      const bz = b.y * GRID_SCALE;
      lines.push([
        new THREE.Vector3(ax, 2.8, az),
        new THREE.Vector3(bx, 2.8, bz),
      ]);
    }
  }

  return (
    <group>
      {lines.map(([start, end], i) => {
        const mid = new THREE.Vector3().addVectors(start, end).multiplyScalar(0.5);
        const len = start.distanceTo(end);
        const angle = Math.atan2(end.x - start.x, end.z - start.z);
        return (
          <mesh key={i} position={[mid.x, mid.y, mid.z]} rotation={[0, angle, 0]}>
            <cylinderGeometry args={[0.15, 0.15, len, 6]} />
            <meshStandardMaterial color="#8a7e70" transparent opacity={0.7} />
          </mesh>
        );
      })}
    </group>
  );
}



// ── Main 3D Editor Component ──
export default function LevelEditor3D({
  elements, activeTool, palette, gridWidth: gw, gridHeight: gh,
  onPlace, onSelect, selectedId,
}: Props) {
  const [ghostPos, setGhostPos] = useState<Vec2>({ x: Math.floor(gw / 2), y: Math.floor(gh / 2) });
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState<Vec2 | null>(null);
  const [dragEnd, setDragEnd] = useState<Vec2 | null>(null);
  const canvasRef = useRef<HTMLDivElement>(null);

  const isWallTool = ['wall', 'glass-wall', 'glass-partition', 'window'].includes(activeTool);

  // Calculate ghost size (for wall drag)
  const ghostW = isWallTool && dragStart && dragEnd
    ? Math.max(1, Math.abs(dragEnd.x - dragStart.x) + 1)
    : sizeFor(activeTool, palette)[0];
  const ghostH = isWallTool && dragStart && dragEnd
    ? Math.max(1, Math.abs(dragEnd.y - dragStart.y) + 1)
    : sizeFor(activeTool, palette)[1];

  // Ghost position for wall drag
  const ghostGX = isWallTool && dragStart && dragEnd
    ? Math.min(dragStart.x, dragEnd.x)
    : ghostPos.x;
  const ghostGY = isWallTool && dragStart && dragEnd
    ? Math.min(dragStart.y, dragEnd.y)
    : ghostPos.y;

  // Validate placement
  const isValid = canPlace(activeTool, ghostGX, ghostGY, ghostW, ghostH, elements, gw, gh);

  const handlePointerMove = useCallback((e: any) => {
    // Raycast to floor plane
    const point = e.point;
    const gx = Math.round(point.x / GRID_SCALE + gw / 2);
    const gz = Math.round(point.z / GRID_SCALE + gh / 2);
    const snapped = {
      x: Math.max(0, Math.min(gw - 1, gx)),
      y: Math.max(0, Math.min(gh - 1, gz)),
    };

    if (isDragging && isWallTool) {
      setDragEnd(snapped);
    } else {
      setGhostPos(snapped);
    }
  }, [gw, gh, isDragging, isWallTool]);

  const handlePointerDown = useCallback((e: any) => {
    if (e.button !== 0) return; // left click only
    e.stopPropagation();

    const point = e.point;
    const gx = Math.round(point.x / GRID_SCALE + gw / 2);
    const gz = Math.round(point.z / GRID_SCALE + gh / 2);
    const snapped = {
      x: Math.max(0, Math.min(gw - 1, gx)),
      y: Math.max(0, Math.min(gh - 1, gz)),
    };

    if (isWallTool) {
      setIsDragging(true);
      setDragStart(snapped);
      setDragEnd(snapped);
    } else {
      // Place immediately
      const [pw, ph] = sizeFor(activeTool, palette);
      if (canPlace(activeTool, snapped.x, snapped.y, pw, ph, elements, gw, gh)) {
        onPlace(activeTool, snapped.x, snapped.y, pw, ph);
      }
    }
  }, [activeTool, palette, elements, gw, gh, isWallTool, onPlace]);

  const handlePointerUp = useCallback(() => {
    if (isDragging && dragStart && dragEnd) {
      const x = Math.min(dragStart.x, dragEnd.x);
      const y = Math.min(dragStart.y, dragEnd.y);
      const w = Math.max(1, Math.abs(dragEnd.x - dragStart.x) + 1);
      const h = Math.max(1, Math.abs(dragEnd.y - dragStart.y) + 1);
      if (canPlace(activeTool, x, y, w, h, elements, gw, gh)) {
        onPlace(activeTool, x, y, w, h);
      }
    }
    setIsDragging(false);
    setDragStart(null);
    setDragEnd(null);
  }, [isDragging, dragStart, dragEnd, activeTool, elements, gw, gh, onPlace]);

  return (
    <div ref={canvasRef} style={{ width: '100%', height: '100%', cursor: 'crosshair' }}>
      <Canvas
        shadows
        camera={{ position: [0, 25, 25], fov: 50 }}
        onPointerMissed={() => onSelect(null)}
      >
        {/* Lighting */}
        <ambientLight intensity={0.5} />
        <directionalLight
          position={[10, 20, 10]}
          intensity={0.8}
          castShadow
          shadow-mapSize-width={2048}
          shadow-mapSize-height={2048}
        />
        <hemisphereLight args={['#fff0e0', '#c0d0e0', 0.3]} />

        {/* Floor and grid */}
        <FloorPlane gw={gw} gh={gh} />

        {/* Existing elements */}
        {elements.map(el => (
          <ElementMesh
            key={el.id}
            element={el}
            palette={palette}
            isSelected={el.id === selectedId}
          />
        ))}

        {/* Ghost preview */}
        <group
          onPointerMove={handlePointerMove}
          onPointerDown={handlePointerDown}
          onPointerUp={handlePointerUp}
        >
          {/* Invisible floor plane for raycasting */}
          <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.01, 0]}>
            <planeGeometry args={[gw * GRID_SCALE, gh * GRID_SCALE]} />
            <meshBasicMaterial visible={false} />
          </mesh>

          <GhostElement
            type={activeTool}
            gx={ghostGX}
            gy={ghostGY}
            gw={ghostW}
            gh={ghostH}
            valid={isValid}
            palette={palette}
          />
        </group>

        {/* Air duct overlay */}
        <DuctOverlay elements={elements} />

        {/* Camera controls */}
        <OrbitControls
          enablePan
          enableRotate
          enableZoom
          maxPolarAngle={Math.PI / 2.2}
          minDistance={5}
          maxDistance={80}
          target={[0, 0, 0]}
        />
      </Canvas>
    </div>
  );
}
