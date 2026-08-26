import { useMemo, useRef, useState } from 'react';
import type { ChangeEvent, CSSProperties, DragEvent as ReactDragEvent, PointerEvent as ReactPointerEvent } from 'react';
import {
  Box,
  ChevronLeft,
  ChevronRight,
  Download,
  Eye,
  FileJson,
  Grid3X3,
  KeyRound,
  Layers3,
  LockKeyhole,
  MousePointer2,
  Paintbrush,
  Play,
  Plus,
  RotateCcw,
  Save,
  Sparkles,
  Trash2,
  Upload,
  Users,
  WandSparkles,
} from 'lucide-react';
import './LevelDesigner.css';

type ElementType =
  | 'wall'
  | 'glass-wall'
  | 'door'
  | 'office'
  | 'cubicle'
  | 'reception'
  | 'desk'
  | 'terminal-desk'
  | 'chair'
  | 'printer'
  | 'stair'
  | 'elevator'
  | 'plant'
  | 'prop';

type ToolCategory = 'structure' | 'workspaces' | 'furniture' | 'dress';
type Department = 'General' | 'Janitorial' | 'IT' | 'HR' | 'Accounts' | 'Sales' | 'Security';
type AccessMethod = 'Steal' | 'Gaslight' | 'Charm' | 'Seduce' | 'Impersonate';

type Vec2 = { x: number; y: number };

interface LevelFloor {
  id: string;
  name: string;
  width: number;
  height: number;
}

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
  departments: Department[];
  accessCardId: string | null;
  gameplay: boolean;
  placeholder: boolean;
}

interface AccessCard {
  id: string;
  name: string;
  holder: string;
  level: number;
  color: string;
  methods: AccessMethod[];
}

interface DressingItem {
  id: string;
  type: 'light' | 'picture' | 'plant' | 'sign' | 'clutter';
  floorId: string;
  x: number;
  y: number;
  label: string;
}

interface WorkshopDocument {
  format: 'office-shift-workshop';
  version: 1;
  business: string;
  floors: LevelFloor[];
  elements: LevelElement[];
  accessCards: AccessCard[];
  dressing: DressingItem[];
}

interface LevelDesignerProps {
  onPlay: () => void;
}

interface PaletteItem {
  type: ElementType;
  label: string;
  icon: typeof Box;
  category: ToolCategory;
  size: [number, number];
  color: string;
  gameplay: boolean;
}

const DEPARTMENTS: Department[] = ['General', 'Janitorial', 'IT', 'HR', 'Accounts', 'Sales', 'Security'];
const ACCESS_METHODS: AccessMethod[] = ['Steal', 'Gaslight', 'Charm', 'Seduce', 'Impersonate'];
const ROOM_NAMES = ['Open office', 'Server room', 'HR suite', 'Meeting room', 'Reception', 'Janitor closet', 'Accounts'];

const palette: PaletteItem[] = [
  { type: 'wall', label: 'Wall', icon: Box, category: 'structure', size: [4, 1], color: 'stone', gameplay: true },
  { type: 'glass-wall', label: 'Glass wall', icon: Eye, category: 'structure', size: [4, 1], color: 'glass', gameplay: true },
  { type: 'door', label: 'Door', icon: LockKeyhole, category: 'structure', size: [2, 1], color: 'door', gameplay: true },
  { type: 'office', label: 'Office', icon: Box, category: 'workspaces', size: [6, 5], color: 'room', gameplay: true },
  { type: 'cubicle', label: 'Cubicle', icon: Grid3X3, category: 'workspaces', size: [4, 4], color: 'cubicle', gameplay: true },
  { type: 'reception', label: 'Reception', icon: Users, category: 'workspaces', size: [7, 3], color: 'reception', gameplay: true },
  { type: 'desk', label: 'Desk', icon: Box, category: 'furniture', size: [3, 2], color: 'desk', gameplay: true },
  { type: 'terminal-desk', label: 'Terminal desk', icon: FileJson, category: 'furniture', size: [3, 2], color: 'terminal', gameplay: true },
  { type: 'chair', label: 'Chair', icon: Box, category: 'furniture', size: [2, 2], color: 'chair', gameplay: true },
  { type: 'printer', label: 'Printer', icon: Box, category: 'furniture', size: [2, 2], color: 'printer', gameplay: true },
  { type: 'stair', label: 'Stairs', icon: Layers3, category: 'workspaces', size: [3, 3], color: 'stairs', gameplay: true },
  { type: 'elevator', label: 'Elevator', icon: Layers3, category: 'workspaces', size: [3, 3], color: 'elevator', gameplay: true },
  { type: 'plant', label: 'Plant', icon: Sparkles, category: 'dress', size: [2, 2], color: 'plant', gameplay: false },
  { type: 'prop', label: 'Prop marker', icon: Paintbrush, category: 'dress', size: [2, 2], color: 'prop', gameplay: false },
];

const categoryLabels: Record<ToolCategory, string> = {
  structure: 'Structure',
  workspaces: 'Rooms & stations',
  furniture: 'Gameplay props',
  dress: 'Dressing',
};

const typeLabels: Record<ElementType, string> = Object.fromEntries(
  palette.map(item => [item.type, item.label]),
) as Record<ElementType, string>;

const initialCards: AccessCard[] = [
  { id: 'card-janitor', name: 'Janitor master', holder: 'Gary / Janitorial', level: 1, color: '#c6a15b', methods: ['Steal', 'Charm', 'Impersonate'] },
  { id: 'card-it', name: 'IT operations', holder: 'Mina / IT', level: 2, color: '#62a8a8', methods: ['Steal', 'Gaslight', 'Impersonate'] },
  { id: 'card-accounts', name: 'Accounts level 3', holder: 'Gary / Accounts', level: 3, color: '#c8755a', methods: ['Gaslight', 'Charm', 'Seduce'] },
  { id: 'card-hr', name: 'HR executive', holder: 'Nadia / HR', level: 4, color: '#8d79b8', methods: ['Charm', 'Seduce', 'Impersonate'] },
];

const initialFloors: LevelFloor[] = [
  { id: 'floor-1', name: 'Ground floor', width: 28, height: 18 },
  { id: 'floor-2', name: 'Operations', width: 28, height: 18 },
];

function idFor(prefix: string): string {
  return `${prefix}-${Math.random().toString(36).slice(2, 8)}`;
}

function starterElements(): LevelElement[] {
  const items: LevelElement[] = [];
  const add = (
    type: ElementType,
    label: string,
    x: number,
    y: number,
    width: number,
    height: number,
    room: string,
    departments: Department[],
    accessCardId: string | null = null,
  ) => {
    const item = palette.find(entry => entry.type === type);
    items.push({
      id: idFor(type), type, label, floorId: 'floor-1', x, y, width, height,
      rotation: 0, room, departments, accessCardId, gameplay: item?.gameplay ?? true, placeholder: true,
    });
  };

  add('reception', 'Main reception', 10, 14, 8, 3, 'Reception', ['General', 'Security']);
  add('wall', 'West wall', 1, 1, 1, 16, 'Open office', ['General']);
  add('wall', 'North wall', 1, 1, 26, 1, 'Open office', ['General']);
  add('wall', 'East wall', 26, 1, 1, 16, 'Open office', ['General']);
  add('wall', 'South wall', 1, 17, 26, 1, 'Reception', ['General']);
  add('door', 'Server access', 4, 1, 2, 1, 'Server room', ['IT'], 'card-it');
  add('office', 'Server room', 2, 3, 7, 5, 'Server room', ['IT'], 'card-it');
  add('terminal-desk', 'Blueprint terminal', 4, 5, 3, 2, 'Server room', ['IT'], 'card-it');
  add('office', 'HR suite', 18, 3, 6, 5, 'HR suite', ['HR'], 'card-hr');
  add('door', 'HR access', 20, 3, 2, 1, 'HR suite', ['HR'], 'card-hr');
  add('cubicle', 'Accounts pod', 11, 4, 4, 4, 'Accounts', ['Accounts'], 'card-accounts');
  add('cubicle', 'Open pod', 11, 10, 4, 4, 'Open office', ['General', 'Sales']);
  add('printer', 'Shared printer', 7, 10, 2, 2, 'Open office', ['General', 'HR']);
  add('desk', 'Reception desk', 11, 14, 4, 2, 'Reception', ['General', 'Security']);
  add('chair', 'Lobby chair', 17, 14, 2, 2, 'Reception', ['General']);
  return items;
}

function initialDressing(): DressingItem[] {
  return [
    { id: idFor('light'), type: 'light', floorId: 'floor-1', x: 8, y: 3, label: 'Fluorescent strip' },
    { id: idFor('light'), type: 'light', floorId: 'floor-1', x: 18, y: 10, label: 'Fluorescent strip' },
    { id: idFor('plant'), type: 'plant', floorId: 'floor-1', x: 23, y: 13, label: 'Office plant' },
    { id: idFor('picture'), type: 'picture', floorId: 'floor-1', x: 10, y: 2, label: 'Motivational print' },
  ];
}

function starterDocument(): WorkshopDocument {
  return {
    format: 'office-shift-workshop', version: 1, business: 'OmniCore Industries',
    floors: initialFloors, elements: starterElements(), accessCards: initialCards, dressing: initialDressing(),
  };
}

function cloneDocument(document: WorkshopDocument): WorkshopDocument {
  return JSON.parse(JSON.stringify(document)) as WorkshopDocument;
}

function paletteForType(type: ElementType): PaletteItem {
  return palette.find(item => item.type === type) ?? palette[0];
}

function snap(value: number, max: number): number {
  return Math.max(1, Math.min(max, Math.round(value)));
}

function elementColor(element: LevelElement): string {
  return paletteForType(element.type).color;
}

function safeJsonDownload(filename: string, value: unknown): void {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

export default function LevelDesigner({ onPlay }: LevelDesignerProps) {
  const [document, setDocument] = useState<WorkshopDocument>(() => starterDocument());
  const [floorId, setFloorId] = useState('floor-1');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [activeTool, setActiveTool] = useState<ElementType>('wall');
  const [category, setCategory] = useState<ToolCategory>('structure');
  const [showGrid, setShowGrid] = useState(true);
  const [showDressing, setShowDressing] = useState(true);
  const [zoom, setZoom] = useState(1);
  const [status, setStatus] = useState('Unsaved workshop');
  const [dragging, setDragging] = useState<{ id: string; offset: Vec2 } | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const canvasRef = useRef<HTMLDivElement>(null);

  const currentFloor = document.floors.find(floor => floor.id === floorId) ?? document.floors[0] ?? {
    id: 'floor-1', name: 'Ground floor', width: 28, height: 18,
  };
  const visibleElements = useMemo(
    () => document.elements.filter(element => element.floorId === currentFloor.id),
    [currentFloor.id, document.elements],
  );
  const selected = document.elements.find(element => element.id === selectedId) ?? null;
  const activePalette = palette.filter(item => item.category === category);
  const validation = useMemo(() => {
    const issues: string[] = [];
    for (const element of document.elements) {
      const floor = document.floors.find(item => item.id === element.floorId);
      if (!floor) issues.push(`${element.label} is on a missing floor`);
      else if (element.x < 1 || element.y < 1 || element.x + element.width - 1 > floor.width || element.y + element.height - 1 > floor.height) issues.push(`${element.label} is outside ${floor.name}`);
      if (element.accessCardId && !document.accessCards.some(card => card.id === element.accessCardId)) issues.push(`${element.label} references a missing access card`);
    }
    const gameplay = document.elements.filter(element => element.gameplay);
    const containers: ElementType[] = ['office', 'cubicle', 'reception'];
    const contains = (outer: LevelElement, inner: LevelElement) =>
      inner.x >= outer.x && inner.y >= outer.y
      && inner.x + inner.width <= outer.x + outer.width
      && inner.y + inner.height <= outer.y + outer.height;
    for (let i = 0; i < gameplay.length; i += 1) {
      for (let j = i + 1; j < gameplay.length; j += 1) {
        const a = gameplay[i];
        const b = gameplay[j];
        if (a.floorId !== b.floorId) continue;
        const overlaps = a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
        const validContainment = containers.includes(a.type) || containers.includes(b.type)
          ? contains(a, b) || contains(b, a)
          : a.type === 'door' || b.type === 'door';
        if (overlaps && !validContainment) issues.push(`${a.label} overlaps ${b.label}`);
      }
    }
    return issues;
  }, [document.accessCards, document.elements, document.floors]);

  const updateDocument = (updater: (draft: WorkshopDocument) => void) => {
    setDocument(previous => {
      const next = cloneDocument(previous);
      updater(next);
      return next;
    });
    setStatus('Unsaved changes');
  };

  const canvasPoint = (event: ReactPointerEvent<HTMLDivElement> | ReactDragEvent<HTMLDivElement>): Vec2 => {
    const rect = canvasRef.current?.getBoundingClientRect() ?? event.currentTarget.getBoundingClientRect();
    return {
      x: snap(((event.clientX - rect.left) / rect.width) * currentFloor.width, currentFloor.width),
      y: snap(((event.clientY - rect.top) / rect.height) * currentFloor.height, currentFloor.height),
    };
  };

  const placeElement = (type: ElementType, point: Vec2) => {
    const item = paletteForType(type);
    const element: LevelElement = {
      id: idFor(type), type, label: item.label, floorId: currentFloor.id,
      x: Math.min(point.x, currentFloor.width - item.size[0] + 1),
      y: Math.min(point.y, currentFloor.height - item.size[1] + 1),
      width: item.size[0], height: item.size[1], rotation: 0, room: 'Open office',
      departments: ['General'], accessCardId: null, gameplay: item.gameplay, placeholder: true,
    };
    updateDocument(draft => draft.elements.push(element));
    setSelectedId(element.id);
  };

  const handleCanvasPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.target !== event.currentTarget) return;
    placeElement(activeTool, canvasPoint(event));
  };

  const handleElementPointerDown = (event: ReactPointerEvent<HTMLDivElement>, element: LevelElement) => {
    event.stopPropagation();
    const point = canvasPoint(event);
    setSelectedId(element.id);
    setDragging({ id: element.id, offset: { x: point.x - element.x, y: point.y - element.y } });
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const handleElementPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!dragging) return;
    const point = canvasPoint(event);
    updateDocument(draft => {
      const item = draft.elements.find(element => element.id === dragging.id);
      if (!item) return;
      item.x = Math.max(1, Math.min(currentFloor.width - item.width + 1, Math.round(point.x - dragging.offset.x)));
      item.y = Math.max(1, Math.min(currentFloor.height - item.height + 1, Math.round(point.y - dragging.offset.y)));
    });
  };

  const stopDragging = () => setDragging(null);

  const updateSelected = (patch: Partial<LevelElement>) => {
    if (!selectedId) return;
    updateDocument(draft => {
      const item = draft.elements.find(element => element.id === selectedId);
      if (item) Object.assign(item, patch);
    });
  };

  const rotateSelected = () => {
    if (!selected) return;
    const nextRotation = ((selected.rotation + 90) % 360) as 0 | 90 | 180 | 270;
    updateSelected({ rotation: nextRotation });
  };

  const deleteSelected = () => {
    if (!selectedId) return;
    updateDocument(draft => {
      draft.elements = draft.elements.filter(element => element.id !== selectedId);
    });
    setSelectedId(null);
  };

  const addFloor = () => {
    const floor: LevelFloor = { id: idFor('floor'), name: `Level ${document.floors.length + 1}`, width: 28, height: 18 };
    updateDocument(draft => draft.floors.push(floor));
    setFloorId(floor.id);
    setSelectedId(null);
  };

  const autoDress = () => {
    const additions: DressingItem[] = [];
    for (let x = 3; x <= currentFloor.width - 2; x += 5) {
      for (let y = 3; y <= currentFloor.height - 2; y += 5) {
        const occupied = visibleElements.some(element => x >= element.x && x < element.x + element.width && y >= element.y && y < element.y + element.height);
        if (occupied) continue;
        const type = (x + y) % 3 === 0 ? 'plant' : (x + y) % 3 === 1 ? 'picture' : 'light';
        additions.push({ id: idFor(type), type, floorId: currentFloor.id, x, y, label: type === 'light' ? 'Fluorescent strip' : type === 'plant' ? 'Office plant' : 'Wall picture' });
      }
    }
    updateDocument(draft => {
      draft.dressing = draft.dressing.filter(item => item.floorId !== currentFloor.id).concat(additions);
    });
    setShowDressing(true);
    setStatus('Procedural dressing generated');
  };

  const saveWorkshop = () => {
    safeJsonDownload(`${document.business.toLowerCase().replaceAll(' ', '-')}-workshop.json`, document);
    setStatus('Workshop JSON exported');
  };

  const saveGodot = () => {
    const godot = {
      format: 'office-shift-godot-level', version: 1, business: document.business,
      floors: document.floors.map(floor => ({ ...floor, elements: document.elements.filter(element => element.floorId === floor.id) })),
      accessCards: document.accessCards,
      dressing: document.dressing,
    };
    safeJsonDownload(`${document.business.toLowerCase().replaceAll(' ', '-')}-godot.json`, godot);
    setStatus('Godot-oriented JSON exported');
  };

  const loadWorkshop = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const parsed = JSON.parse(String(reader.result)) as WorkshopDocument;
        if (parsed.format !== 'office-shift-workshop' || !Array.isArray(parsed.floors) || !Array.isArray(parsed.elements)) throw new Error('Invalid workshop document');
        const normalized: WorkshopDocument = {
          ...parsed,
          accessCards: (Array.isArray(parsed.accessCards) ? parsed.accessCards : initialCards).map(card => ({ ...card, methods: Array.isArray(card.methods) && card.methods.length ? card.methods : ['Steal'] })),
          dressing: Array.isArray(parsed.dressing) ? parsed.dressing : [],
        };
        setDocument(normalized);
        setFloorId(normalized.floors[0]?.id ?? 'floor-1');
        setSelectedId(null);
        setStatus('Workshop imported');
      } catch {
        setStatus('Import failed: not a workshop JSON');
      }
    };
    reader.readAsText(file);
    event.target.value = '';
  };

  const addCard = () => {
    const card: AccessCard = { id: idFor('card'), name: 'New access card', holder: 'Unassigned', level: 1, color: '#8490a0', methods: ['Steal'] };
    updateDocument(draft => draft.accessCards.push(card));
  };

  const updateCard = (id: string, patch: Partial<AccessCard>) => {
    updateDocument(draft => {
      const card = draft.accessCards.find(item => item.id === id);
      if (card) Object.assign(card, patch);
    });
  };

  const toggleCardMethod = (card: AccessCard, method: AccessMethod) => {
    updateCard(card.id, { methods: card.methods.includes(method) ? card.methods.filter(item => item !== method) : [...card.methods, method] });
  };

  const updateFloor = (patch: Partial<LevelFloor>) => {
    updateDocument(draft => {
      const floor = draft.floors.find(item => item.id === floorId);
      if (floor) Object.assign(floor, patch);
    });
  };

  const switchFloor = (direction: number) => {
    const index = document.floors.findIndex(floor => floor.id === floorId);
    const next = document.floors[(index + direction + document.floors.length) % document.floors.length];
    if (next) {
      setFloorId(next.id);
      setSelectedId(null);
    }
  };

  return (
    <main className="workshop-shell">
      <header className="workshop-header">
        <div className="brand-lockup">
          <div className="brand-mark"><Grid3X3 size={18} /></div>
          <div>
            <div className="eyebrow">OFFICE SHIFT / WORKSHOP</div>
            <h1>Infiltration level designer</h1>
          </div>
        </div>
        <div className="header-actions">
          <span className="save-state"><span className="state-dot" /> {status}</span>
          <button className="tool-button quiet" title="Import a workshop JSON" onClick={() => fileRef.current?.click()}><Upload size={16} /> Import</button>
          <button className="tool-button quiet" title="Play the current prototype" onClick={onPlay}><Play size={16} /> Play</button>
          <input ref={fileRef} type="file" accept="application/json" onChange={loadWorkshop} hidden />
        </div>
      </header>

      <section className="workshop-layout">
        <aside className="left-rail">
          <div className="rail-heading"><span>Build palette</span><span className="count-label">{document.elements.length} items</span></div>
          <div className="category-tabs">
            {(Object.keys(categoryLabels) as ToolCategory[]).map(tab => (
              <button key={tab} className={category === tab ? 'active' : ''} onClick={() => setCategory(tab)}>{categoryLabels[tab]}</button>
            ))}
          </div>
          <div className="palette-list">
            {activePalette.map(item => {
              const Icon = item.icon;
              return (
                <button
                  className={`palette-item ${activeTool === item.type ? 'selected' : ''}`}
                  key={item.type}
                  draggable
                  title={`Place ${item.label}. ${item.gameplay ? 'Gameplay element.' : 'Dressing only; no gameplay collision.'}`}
                  onClick={() => setActiveTool(item.type)}
                  onDragStart={event => event.dataTransfer.setData('application/x-office-shift-tool', item.type)}
                >
                  <span className={`palette-icon palette-${item.color}`}><Icon size={16} /></span>
                  <span>{item.label}</span>
                  <small>{item.gameplay ? 'GAMEPLAY' : 'DRESS'}</small>
                </button>
              );
            })}
          </div>
          <div className="rail-tip"><MousePointer2 size={15} /><span>Click a tool, then click the grid. Drag items to reposition.</span></div>
        </aside>

        <section className="canvas-stage">
          <div className="canvas-toolbar">
            <div className="floor-switcher">
              <button className="icon-button" title="Previous floor" onClick={() => switchFloor(-1)}><ChevronLeft size={16} /></button>
              <Layers3 size={16} />
              <select value={floorId} onChange={event => { setFloorId(event.target.value); setSelectedId(null); }} aria-label="Active floor">
                {document.floors.map(floor => <option key={floor.id} value={floor.id}>{floor.name}</option>)}
              </select>
              <button className="icon-button" title="Next floor" onClick={() => switchFloor(1)}><ChevronRight size={16} /></button>
              <button className="icon-button add-floor" title="Add floor" onClick={addFloor}><Plus size={16} /></button>
            </div>
            <div className="canvas-tools">
              <button className={`toggle-button ${showGrid ? 'on' : ''}`} onClick={() => setShowGrid(value => !value)}><Grid3X3 size={15} /> Grid</button>
              <button className={`toggle-button ${showDressing ? 'on' : ''}`} onClick={() => setShowDressing(value => !value)}><Eye size={15} /> Dressing</button>
              <button className="icon-button" title="Zoom out" onClick={() => setZoom(value => Math.max(0.8, value - 0.1))}>−</button>
              <span className="zoom-label">{Math.round(zoom * 100)}%</span>
              <button className="icon-button" title="Zoom in" onClick={() => setZoom(value => Math.min(1.2, value + 0.1))}>+</button>
            </div>
          </div>
          <div className="canvas-meta">
            <div><span className="meta-kicker">ACTIVE PLAN</span><strong>{currentFloor.name}</strong><span className="meta-divider" />{currentFloor.width} × {currentFloor.height} cells</div>
            <div className="legend"><span className={validation.length ? 'validation-warning' : 'validation-ok'}>{validation.length ? `${validation.length} validation issue${validation.length === 1 ? '' : 's'}` : 'Plan validates'}</span><span><i className="legend-swatch gameplay" /> Gameplay</span><span><i className="legend-swatch dress" /> Dressing only</span></div>
          </div>
          <div className="canvas-wrap">
            <div
              ref={canvasRef}
              className={`level-canvas ${showGrid ? 'grid-visible' : ''}`}
              style={{ '--grid-w': currentFloor.width, '--grid-h': currentFloor.height, '--zoom': zoom } as CSSProperties}
              onPointerDown={handleCanvasPointerDown}
              onDragOver={event => event.preventDefault()}
              onDrop={event => {
                event.preventDefault();
                const dropped = event.dataTransfer.getData('application/x-office-shift-tool') as ElementType;
                if (dropped) placeElement(dropped, canvasPoint(event));
              }}
            >
              <div className="canvas-label top-left">NORTH / RECEPTION STRIP</div>
              {showDressing && document.dressing.filter(item => item.floorId === currentFloor.id).map(item => (
                <div key={item.id} className={`dressing-marker dressing-${item.type}`} style={{ left: `${((item.x - 1) / currentFloor.width) * 100}%`, top: `${((item.y - 1) / currentFloor.height) * 100}%` }} title={`${item.label} · dressing only`}>
                  {item.type === 'light' ? '▰' : item.type === 'plant' ? '✦' : '▣'}
                </div>
              ))}
              {visibleElements.map(element => {
                const isSelected = element.id === selectedId;
                return (
                  <div
                    key={element.id}
                    className={`layout-element element-${elementColor(element)} ${isSelected ? 'is-selected' : ''} ${element.gameplay ? '' : 'non-gameplay'}`}
                    style={{
                      left: `${((element.x - 1) / currentFloor.width) * 100}%`, top: `${((element.y - 1) / currentFloor.height) * 100}%`,
                      width: `${(element.width / currentFloor.width) * 100}%`, height: `${(element.height / currentFloor.height) * 100}%`,
                      transform: `rotate(${element.rotation}deg)`,
                    }}
                    onPointerDown={event => handleElementPointerDown(event, element)}
                    onPointerMove={handleElementPointerMove}
                    onPointerUp={stopDragging}
                    onDoubleClick={event => { event.stopPropagation(); setSelectedId(element.id); }}
                    title={`${element.label} · ${element.room} · ${element.gameplay ? 'gameplay' : 'dressing only'}`}
                  >
                    <span className="element-label">{element.label}</span>
                    {element.accessCardId && <KeyRound className="element-key" size={12} />}
                  </div>
                );
              })}
              <div className="scale-ruler"><span>0</span><span>5 cells</span><span>10</span></div>
            </div>
          </div>
          <div className="canvas-footer"><span><MousePointer2 size={13} /> {selected ? `${selected.label} selected` : 'Select an item to edit properties'}</span><span>Snap: 1 cell <span className="footer-separator">·</span> Collisions use element footprint</span></div>
        </section>

        <aside className="right-rail">
          <div className="inspector-section inspector-top"><div className="rail-heading"><span>Inspector</span><span className="selection-state">{selected ? '1 selected' : 'Nothing selected'}</span></div>
            {selected ? (
              <div className="selection-summary"><span className={`summary-icon palette-${elementColor(selected)}`}><Box size={16} /></span><div><strong>{selected.label}</strong><small>{typeLabels[selected.type]} · {selected.gameplay ? 'Gameplay' : 'Dressing only'}</small></div><button className="icon-button danger-icon" title="Delete selected element" onClick={deleteSelected}><Trash2 size={15} /></button></div>
            ) : <div className="empty-inspector"><MousePointer2 size={20} /><span>Click any placed item<br />to inspect it here.</span></div>}
          </div>
          {!selected && <div className="inspector-fields floor-fields">
            <label className="field-label">Business name<input value={document.business} onChange={event => updateDocument(draft => { draft.business = event.target.value; })} /></label>
            <label className="field-label">Floor name<input value={currentFloor.name} onChange={event => updateFloor({ name: event.target.value })} /></label>
            <div className="field-row"><label className="field-label">Grid width<input type="number" min={12} max={60} value={currentFloor.width} onChange={event => updateFloor({ width: Math.max(12, Math.min(60, Number(event.target.value))) })} /></label><label className="field-label">Grid height<input type="number" min={10} max={40} value={currentFloor.height} onChange={event => updateFloor({ height: Math.max(10, Math.min(40, Number(event.target.value))) })} /></label></div>
            <div className="field-note">Every placement snaps to this grid. Keep a clear aisle from the reception strip to each access-controlled room.</div>
          </div>}
          {selected && <div className="inspector-fields">
            <label className="field-label">Label<input value={selected.label} onChange={event => updateSelected({ label: event.target.value })} /></label>
            <div className="field-row"><label className="field-label">X<input type="number" min={1} value={selected.x} onChange={event => updateSelected({ x: Math.max(1, Number(event.target.value)) })} /></label><label className="field-label">Y<input type="number" min={1} value={selected.y} onChange={event => updateSelected({ y: Math.max(1, Number(event.target.value)) })} /></label></div>
            <div className="field-row"><label className="field-label">Width<input type="number" min={1} value={selected.width} onChange={event => updateSelected({ width: Math.max(1, Number(event.target.value)) })} /></label><label className="field-label">Height<input type="number" min={1} value={selected.height} onChange={event => updateSelected({ height: Math.max(1, Number(event.target.value)) })} /></label></div>
            <button className="secondary-action" onClick={rotateSelected}><RotateCcw size={14} /> Rotate 90° <span>R</span></button>
            <label className="field-label">Designated room<select value={selected.room} onChange={event => updateSelected({ room: event.target.value })}>{ROOM_NAMES.map(room => <option key={room}>{room}</option>)}</select></label>
            <div className="field-label">Departments<div className="tag-grid">{DEPARTMENTS.map(department => <button key={department} className={`tag-button ${selected.departments.includes(department) ? 'active' : ''}`} onClick={() => updateSelected({ departments: selected.departments.includes(department) ? selected.departments.filter(item => item !== department) : [...selected.departments, department] })}>{department}</button>)}</div></div>
            <label className="field-label">Required access card<select value={selected.accessCardId ?? ''} onChange={event => updateSelected({ accessCardId: event.target.value || null })}><option value="">Public / unlocked</option>{document.accessCards.map(card => <option key={card.id} value={card.id}>Level {card.level} · {card.name}</option>)}</select></label>
            <label className="check-line"><input type="checkbox" checked={selected.gameplay} onChange={event => updateSelected({ gameplay: event.target.checked })} /><span>Gameplay collision / interaction</span></label>
            <div className="field-note">Placeholder geometry keeps this footprint when an authored asset replaces it.</div>
          </div>}

          <div className="inspector-section access-section"><div className="rail-heading"><span>Access cards</span><button className="icon-button add-floor" title="Add access card" onClick={addCard}><Plus size={15} /></button></div><div className="access-list">{document.accessCards.map(card => <div className="access-card-row" key={card.id}><span className="card-chip" style={{ background: card.color }}><KeyRound size={13} /></span><div className="access-card-info"><input value={card.name} onChange={event => updateCard(card.id, { name: event.target.value })} /><small><input value={card.holder} onChange={event => updateCard(card.id, { holder: event.target.value })} /></small><div className="method-chips">{ACCESS_METHODS.map(method => <button key={method} className={`method-chip ${card.methods.includes(method) ? 'active' : ''}`} onClick={() => toggleCardMethod(card, method)}>{method}</button>)}</div></div><select value={card.level} onChange={event => updateCard(card.id, { level: Number(event.target.value) })} aria-label={`${card.name} clearance level`}><option value={1}>L1</option><option value={2}>L2</option><option value={3}>L3</option><option value={4}>L4</option></select></div>)}</div><div className="access-note"><LockKeyhole size={14} /><span>Cards are the gate. Tag a room or door, then route access through stealing, charm, impersonation, or social engineering.</span></div></div>

          <div className="inspector-section dressing-section"><div className="rail-heading"><span>Fast dressing</span><Sparkles size={15} className="gold-icon" /></div><p>Populate safe visual detail without changing collision, access, or AI navigation.</p><button className="dress-button" onClick={autoDress}><WandSparkles size={15} /> Procedural generate props</button></div>
        </aside>
      </section>

      <footer className="workshop-footer"><div className="footer-context"><span className="context-icon"><Layers3 size={14} /></span><div><strong>{document.business}</strong><span>{document.floors.length} floors <i /> {document.elements.filter(element => element.gameplay).length} gameplay elements <i /> {document.dressing.length} dressing items</span></div></div><div className="footer-actions"><button className="footer-button" onClick={() => setDocument(starterDocument())}><FileJson size={15} /> Reset template</button><button className="footer-button" onClick={saveWorkshop}><Download size={15} /> Workshop JSON</button><button className="footer-button primary" onClick={saveGodot}><Save size={15} /> Export for Godot</button></div></footer>
    </main>
  );
}
