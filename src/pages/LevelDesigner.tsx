import { useMemo, useRef, useState } from 'react';
import type { ChangeEvent, CSSProperties, DragEvent as ReactDragEvent, PointerEvent as ReactPointerEvent } from 'react';
import { lazy, Suspense } from 'react';
import {
  Armchair,
  BookOpen,
  Box,
  ChevronLeft,
  ChevronRight,
  Clock,
  Coffee,
  Download,
  Droplets,
  Eye,
  FileJson,
  Grid3X3,
  KeyRound,
  Layers3,
  LockKeyhole,
  Monitor,
  MousePointer2,
  Package,
  Paintbrush,
  Play,
  Plus,
  RotateCcw,
  Save,
  ScanLine,
  Server,
  Shield,
  Shredder,
  Sparkles,
  Table,
  Trash2,
  Upload,
  Users,
  WandSparkles,
  Zap,
} from 'lucide-react';
import './LevelDesigner.css';
const LevelEditor3D = lazy(() => import('../components/LevelEditor3D'));

type ElementType =
  // Structure
  | 'wall' | 'glass-wall' | 'glass-partition' | 'door' | 'keycard-door' | 'window' | 'column'
  // Rooms
  | 'office' | 'cubicle' | 'reception' | 'meeting-room' | 'server-room' | 'break-room'
  | 'bathroom' | 'storage-closet' | 'executive-office'
  // Furniture
  | 'desk' | 'terminal-desk' | 'chair' | 'printer' | 'meeting-table' | 'whiteboard'
  | 'bookshelf' | 'filing-cabinet' | 'water-cooler' | 'coffee-machine' | 'vending-machine'
  | 'sofa' | 'lounge-chair' | 'coffee-table' | 'server-rack' | 'safe' | 'shredder'
  | 'scanner' | 'monitor' | 'projector' | 'tv-screen'
  // Vertical
  | 'stair' | 'elevator'
  // Infiltration
  | 'air-duct' | 'duct-vent' | 'vent-access' | 'hiding-nook' | 'body-disposal'
  // Dressing
  | 'plant' | 'prop' | 'clock' | 'fire-extinguisher' | 'coat-rack' | 'umbrella-stand';

type ToolCategory = 'structure' | 'rooms' | 'furniture' | 'tech' | 'breakroom' | 'vertical' | 'infiltration' | 'dress';
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

interface StaffAssignment {
  id: string;
  name: string;
  job: string;
  department: Department;
  floorId: string;
  x: number;
  y: number;
  homeElementId: string | null;
  waypointTags: string[];
  isExecutiveThreat: boolean;
}

interface WorkshopFloorLink {
  id: string;
  fromFloor: string;
  toFloor: string;
  elementId: string;
}

interface WorkshopWaypoint {
  id: string;
  floorId: string;
  label: string;
  x: number;
  y: number;
  tags: string[];
  capacity: number;
  visibility: number;
  socialValue: number;
  coverValue: number;
}

interface WorkshopDocument {
  format: 'office-shift-workshop';
  version: 2;
  business: string;
  floors: LevelFloor[];
  elements: LevelElement[];
  accessCards: AccessCard[];
  staff: StaffAssignment[];
  waypoints: WorkshopWaypoint[];
  floorLinks: WorkshopFloorLink[];
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
const CANONICAL_STAFF = ['Bob', 'Sleepy Steve', 'Pam', 'Mr Purple', 'Fran', 'Chad', 'Rita', 'Mailroom Mike', 'Dave', 'Liz', 'Nervous Ned', 'Manager Mo', 'Jen', 'Data Dave', 'Boring Bill', 'Boss Barbara', 'Joe', 'Kevin', 'Old Tom'];
const ACCESS_METHODS: AccessMethod[] = ['Steal', 'Gaslight', 'Charm', 'Seduce', 'Impersonate'];
const ROOM_NAMES = ['Open office', 'Server room', 'HR suite', 'Meeting room', 'Reception', 'Janitor closet', 'Accounts', 'Break room', 'Bathroom', 'Storage', 'Executive office', 'Security office', 'Marketing', 'Sales floor', 'Legal', 'Finance', 'Mailroom', 'Kitchen', 'Conference room', 'Training room', 'Quiet room', 'Phone booth', 'Server closet', 'Archive', 'Supply room'];

const palette: PaletteItem[] = [
  // ── Structure ──
  { type: 'wall', label: 'Wall', icon: Box, category: 'structure', size: [4, 1], color: 'stone', gameplay: true },
  { type: 'glass-wall', label: 'Glass wall', icon: Eye, category: 'structure', size: [4, 1], color: 'glass', gameplay: true },
  { type: 'glass-partition', label: 'Glass partition', icon: Eye, category: 'structure', size: [3, 1], color: 'glass', gameplay: true },
  { type: 'door', label: 'Door', icon: LockKeyhole, category: 'structure', size: [2, 1], color: 'door', gameplay: true },
  { type: 'keycard-door', label: 'Keycard door', icon: KeyRound, category: 'structure', size: [2, 1], color: 'keycard-door', gameplay: true },
  { type: 'window', label: 'Window', icon: Eye, category: 'structure', size: [3, 1], color: 'window', gameplay: false },
  { type: 'column', label: 'Column', icon: Box, category: 'structure', size: [1, 1], color: 'stone', gameplay: false },
  // ── Rooms ──
  { type: 'office', label: 'Office', icon: Box, category: 'rooms', size: [6, 5], color: 'room', gameplay: true },
  { type: 'cubicle', label: 'Cubicle', icon: Grid3X3, category: 'rooms', size: [4, 4], color: 'cubicle', gameplay: true },
  { type: 'reception', label: 'Reception', icon: Users, category: 'rooms', size: [7, 3], color: 'reception', gameplay: true },
  { type: 'meeting-room', label: 'Meeting room', icon: Users, category: 'rooms', size: [8, 5], color: 'meeting', gameplay: true },
  { type: 'server-room', label: 'Server room', icon: Zap, category: 'rooms', size: [6, 4], color: 'server', gameplay: true },
  { type: 'break-room', label: 'Break room', icon: Coffee, category: 'rooms', size: [6, 4], color: 'breakroom', gameplay: true },
  { type: 'bathroom', label: 'Bathroom', icon: Droplets, category: 'rooms', size: [4, 3], color: 'bathroom', gameplay: true },
  { type: 'storage-closet', label: 'Storage closet', icon: Package, category: 'rooms', size: [3, 3], color: 'storage', gameplay: true },
  { type: 'executive-office', label: 'Executive office', icon: Box, category: 'rooms', size: [8, 6], color: 'executive', gameplay: true },
  // ── Furniture ──
  { type: 'desk', label: 'Desk', icon: Box, category: 'furniture', size: [3, 2], color: 'desk', gameplay: true },
  { type: 'terminal-desk', label: 'Terminal desk', icon: FileJson, category: 'furniture', size: [3, 2], color: 'terminal', gameplay: true },
  { type: 'chair', label: 'Chair', icon: Armchair, category: 'furniture', size: [2, 2], color: 'chair', gameplay: true },
  { type: 'printer', label: 'Printer', icon: Box, category: 'furniture', size: [2, 2], color: 'printer', gameplay: true },
  { type: 'meeting-table', label: 'Meeting table', icon: Table, category: 'furniture', size: [6, 3], color: 'meeting-table', gameplay: true },
  { type: 'whiteboard', label: 'Whiteboard', icon: Paintbrush, category: 'furniture', size: [4, 1], color: 'whiteboard', gameplay: true },
  { type: 'bookshelf', label: 'Bookshelf', icon: BookOpen, category: 'furniture', size: [3, 1], color: 'bookshelf', gameplay: true },
  { type: 'filing-cabinet', label: 'Filing cabinet', icon: Box, category: 'furniture', size: [2, 1], color: 'filing', gameplay: true },
  { type: 'sofa', label: 'Sofa', icon: Armchair, category: 'furniture', size: [4, 2], color: 'sofa', gameplay: false },
  { type: 'lounge-chair', label: 'Lounge chair', icon: Armchair, category: 'furniture', size: [2, 2], color: 'lounge', gameplay: false },
  { type: 'coffee-table', label: 'Coffee table', icon: Table, category: 'furniture', size: [3, 2], color: 'coffee-table', gameplay: false },
  { type: 'server-rack', label: 'Server rack', icon: Server, category: 'furniture', size: [2, 2], color: 'server-rack', gameplay: true },
  { type: 'safe', label: 'Safe', icon: Shield, category: 'furniture', size: [2, 2], color: 'safe', gameplay: true },
  { type: 'shredder', label: 'Shredder', icon: Shredder, category: 'furniture', size: [2, 1], color: 'shredder', gameplay: true },
  { type: 'scanner', label: 'Scanner', icon: ScanLine, category: 'furniture', size: [2, 1], color: 'scanner', gameplay: true },
  { type: 'monitor', label: 'Monitor', icon: Monitor, category: 'furniture', size: [2, 1], color: 'monitor', gameplay: true },
  { type: 'projector', label: 'Projector', icon: Monitor, category: 'furniture', size: [2, 2], color: 'projector', gameplay: true },
  { type: 'tv-screen', label: 'TV screen', icon: Monitor, category: 'furniture', size: [3, 1], color: 'tv', gameplay: true },
  // ── Breakroom ──
  { type: 'water-cooler', label: 'Water cooler', icon: Droplets, category: 'breakroom', size: [2, 2], color: 'water-cooler', gameplay: true },
  { type: 'coffee-machine', label: 'Coffee machine', icon: Coffee, category: 'breakroom', size: [2, 2], color: 'coffee', gameplay: true },
  { type: 'vending-machine', label: 'Vending machine', icon: Package, category: 'breakroom', size: [2, 2], color: 'vending', gameplay: true },
  // ── Vertical ──
  { type: 'stair', label: 'Stairs', icon: Layers3, category: 'vertical', size: [3, 3], color: 'stairs', gameplay: true },
  { type: 'elevator', label: 'Elevator', icon: Layers3, category: 'vertical', size: [3, 3], color: 'elevator', gameplay: true },
  // ── Infiltration ──
  { type: 'air-duct', label: 'Air duct', icon: Box, category: 'infiltration', size: [3, 1], color: 'duct', gameplay: true },
  { type: 'duct-vent', label: 'Duct vent', icon: Box, category: 'infiltration', size: [2, 2], color: 'vent', gameplay: true },
  { type: 'vent-access', label: 'Vent access', icon: Box, category: 'infiltration', size: [2, 2], color: 'vent-access', gameplay: true },
  { type: 'hiding-nook', label: 'Hiding nook', icon: Box, category: 'infiltration', size: [2, 2], color: 'nook', gameplay: true },
  { type: 'body-disposal', label: 'Body disposal', icon: Box, category: 'infiltration', size: [2, 2], color: 'disposal', gameplay: true },
  // ── Dressing ──
  { type: 'plant', label: 'Plant', icon: Sparkles, category: 'dress', size: [2, 2], color: 'plant', gameplay: false },
  { type: 'prop', label: 'Prop marker', icon: Paintbrush, category: 'dress', size: [2, 2], color: 'prop', gameplay: false },
  { type: 'clock', label: 'Wall clock', icon: Clock, category: 'dress', size: [1, 1], color: 'clock', gameplay: false },
  { type: 'fire-extinguisher', label: 'Fire extinguisher', icon: Zap, category: 'dress', size: [1, 1], color: 'fire-ext', gameplay: true },
  { type: 'coat-rack', label: 'Coat rack', icon: Package, category: 'dress', size: [1, 2], color: 'coat-rack', gameplay: false },
  { type: 'umbrella-stand', label: 'Umbrella stand', icon: Package, category: 'dress', size: [1, 1], color: 'umbrella', gameplay: false },
];

const categoryLabels: Record<ToolCategory, string> = {
  structure: 'Structure',
  rooms: 'Rooms',
  furniture: 'Furniture',
  tech: 'Tech & equipment',
  breakroom: 'Break room',
  vertical: 'Vertical',
  infiltration: 'Infiltration',
  dress: 'Dressing',
};

const typeLabels: Record<ElementType, string> = Object.fromEntries(
  palette.map(item => [item.type, item.label]),
) as Record<ElementType, string>;

const initialCards: AccessCard[] = [
  { id: 'card-janitor', name: 'Janitor master', holder: 'Joe / Janitorial', level: 1, color: '#c6a15b', methods: ['Steal', 'Charm', 'Impersonate'] },
  { id: 'card-it', name: 'IT operations', holder: 'Sleepy Steve / IT', level: 2, color: '#62a8a8', methods: ['Steal', 'Gaslight', 'Impersonate'] },
  { id: 'card-accounts', name: 'Accounts level 3', holder: 'Bob / Accounts', level: 3, color: '#c8755a', methods: ['Gaslight', 'Charm', 'Seduce'] },
  { id: 'card-hr', name: 'HR executive', holder: 'Pam / HR', level: 4, color: '#8d79b8', methods: ['Charm', 'Seduce', 'Impersonate'] },
  { id: 'card-security', name: 'Security access', holder: 'Nervous Ned / Security', level: 2, color: '#7a8a6e', methods: ['Steal', 'Gaslight'] },
  { id: 'card-executive', name: 'Executive override', holder: 'Mr Purple / CEO', level: 5, color: '#9b5fb5', methods: ['Steal', 'Impersonate'] },
  { id: 'card-reception', name: 'Reception visitor', holder: 'Rita / Reception', level: 1, color: '#d4a853', methods: ['Charm', 'Seduce'] },
  { id: 'card-procurement', name: 'Procurement access', holder: 'Kevin / Procurement', level: 2, color: '#b87333', methods: ['Steal', 'Gaslight'] },
  { id: 'card-legal', name: 'Legal confidential', holder: 'Dave / Legal', level: 3, color: '#6b7b8d', methods: ['Gaslight', 'Impersonate'] },
  { id: 'card-executive-override', name: 'Master keycard', holder: 'Unknown', level: 6, color: '#e8d44d', methods: ['Steal'] },
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

  // ── Perimeter walls ──
  add('wall', 'West wall', 1, 1, 1, 16, 'Open office', ['General']);
  add('wall', 'North wall', 1, 1, 26, 1, 'Open office', ['General']);
  add('wall', 'East wall', 26, 1, 1, 16, 'Open office', ['General']);
  add('wall', 'South wall', 1, 17, 26, 1, 'Reception', ['General']);

  // ── Reception ──
  add('reception', 'Main reception', 10, 14, 8, 3, 'Reception', ['General', 'Security']);
  add('desk', 'Reception desk', 11, 14, 4, 2, 'Reception', ['General', 'Security']);
  add('chair', 'Lobby chair', 17, 14, 2, 2, 'Reception', ['General']);
  add('sofa', 'Waiting sofa', 10, 15, 4, 2, 'Reception', ['General']);
  add('plant', 'Lobby plant', 18, 15, 2, 2, 'Reception', ['General']);

  // ── Server room (IT) ──
  add('server-room', 'Server room', 2, 3, 7, 5, 'Server room', ['IT'], 'card-it');
  add('keycard-door', 'Server access', 4, 3, 2, 1, 'Server room', ['IT'], 'card-it');
  add('server-rack', 'Rack A', 3, 4, 2, 2, 'Server room', ['IT'], 'card-it');
  add('server-rack', 'Rack B', 5, 4, 2, 2, 'Server room', ['IT'], 'card-it');
  add('terminal-desk', 'IT terminal', 3, 6, 3, 2, 'Server room', ['IT'], 'card-it');
  add('monitor', 'Status monitor', 6, 6, 2, 1, 'Server room', ['IT'], 'card-it');

  // ── HR suite ──
  add('office', 'HR suite', 18, 3, 6, 5, 'HR suite', ['HR'], 'card-hr');
  add('keycard-door', 'HR access', 20, 3, 2, 1, 'HR suite', ['HR'], 'card-hr');
  add('desk', 'HR desk', 19, 5, 3, 2, 'HR suite', ['HR'], 'card-hr');
  add('filing-cabinet', 'Personnel files', 22, 4, 2, 1, 'HR suite', ['HR'], 'card-hr');
  add('chair', 'Interview chair', 21, 6, 2, 2, 'HR suite', ['HR'], 'card-hr');

  // ── Executive office ──
  add('executive-office', 'CEO office', 2, 10, 8, 6, 'Executive office', ['Security'], 'card-executive');
  add('keycard-door', 'Executive access', 4, 10, 2, 1, 'Executive office', ['Security'], 'card-executive');
  add('desk', 'Executive desk', 4, 12, 4, 2, 'Executive office', ['Security'], 'card-executive');
  add('safe', 'Executive safe', 8, 11, 2, 2, 'Executive office', ['Security'], 'card-executive');
  add('plant', 'Executive plant', 3, 14, 2, 2, 'Executive office', ['Security']);

  // ── Cubicle farm ──
  add('cubicle', 'Accounts pod', 11, 4, 4, 4, 'Accounts', ['Accounts'], 'card-accounts');
  add('cubicle', 'Sales pod', 16, 4, 4, 4, 'Sales', ['Sales']);
  add('cubicle', 'Open pod A', 11, 9, 4, 4, 'Open office', ['General']);
  add('cubicle', 'Open pod B', 16, 9, 4, 4, 'Open office', ['General']);

  // ── Meeting room ──
  add('meeting-room', 'Conference room', 18, 10, 8, 6, 'Meeting room', ['General']);
  add('glass-wall', 'Glass front', 18, 10, 8, 1, 'Meeting room', ['General']);
  add('meeting-table', 'Conference table', 20, 12, 5, 3, 'Meeting room', ['General']);
  add('whiteboard', 'Presentation board', 25, 11, 1, 4, 'Meeting room', ['General']);
  add('projector', 'Ceiling projector', 22, 11, 2, 2, 'Meeting room', ['General']);

  // ── Break room ──
  add('break-room', 'Staff kitchen', 2, 14, 6, 3, 'Break room', ['General']);
  add('coffee-machine', 'Coffee station', 3, 14, 2, 2, 'Break room', ['General']);
  add('vending-machine', 'Vending', 5, 14, 2, 2, 'Break room', ['General']);
  add('water-cooler', 'Water cooler', 3, 16, 2, 2, 'Break room', ['General']);

  // ── Open office furniture ──
  add('printer', 'Shared printer', 7, 9, 2, 2, 'Open office', ['General']);
  add('terminal-desk', 'Hot desk', 7, 4, 3, 2, 'Open office', ['General']);
  add('desk', 'Admin desk', 7, 6, 3, 2, 'Open office', ['General']);
  add('bookshelf', 'Reference shelf', 1, 8, 3, 1, 'Open office', ['General']);

  // ── Vertical ──
  add('elevator', 'Main elevator', 12, 16, 3, 2, 'Reception', ['General']);
  add('stair', 'Fire stairs', 24, 16, 3, 2, 'Open office', ['General']);

  // ── Dressing ──
  add('plant', 'Corner plant', 1, 1, 2, 2, 'Open office', ['General']);
  add('plant', 'Hallway plant', 26, 8, 2, 2, 'Open office', ['General']);

  return items;
}

function initialDressing(): DressingItem[] {
  return [
    { id: idFor('light'), type: 'light', floorId: 'floor-1', x: 8, y: 3, label: 'Fluorescent strip' },
    { id: idFor('light'), type: 'light', floorId: 'floor-1', x: 18, y: 10, label: 'Fluorescent strip' },
    { id: idFor('light'), type: 'light', floorId: 'floor-1', x: 5, y: 12, label: 'Fluorescent strip' },
    { id: idFor('plant'), type: 'plant', floorId: 'floor-1', x: 23, y: 13, label: 'Office plant' },
    { id: idFor('plant'), type: 'plant', floorId: 'floor-1', x: 9, y: 8, label: 'Hallway fern' },
    { id: idFor('picture'), type: 'picture', floorId: 'floor-1', x: 10, y: 2, label: 'Motivational print' },
    { id: idFor('sign'), type: 'sign', floorId: 'floor-1', x: 12, y: 1, label: 'Floor directory' },
    { id: idFor('clutter'), type: 'clutter', floorId: 'floor-1', x: 7, y: 11, label: 'Stack of reports' },
    { id: idFor('clutter'), type: 'clutter', floorId: 'floor-1', x: 15, y: 6, label: 'Coffee mug' },
    { id: idFor('light'), type: 'light', floorId: 'floor-2', x: 10, y: 8, label: 'Fluorescent strip' },
    { id: idFor('plant'), type: 'plant', floorId: 'floor-2', x: 3, y: 3, label: 'Executive orchid' },
  ];
}

function starterDocument(): WorkshopDocument {
  const elements = starterElements();
  return {
    format: 'office-shift-workshop', version: 2, business: 'OmniCore Industries',
    floors: initialFloors, elements, accessCards: initialCards,
    staff: [], waypoints: [], floorLinks: [], dressing: initialDressing(),
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
  const [selectedRecord, setSelectedRecord] = useState<{ kind: 'element' | 'staff' | 'waypoint'; id: string } | null>(null);
  const [activeTool, setActiveTool] = useState<ElementType>('wall');
  const [category, setCategory] = useState<ToolCategory>('structure');
  const [showGrid, setShowGrid] = useState(true);
  const [showDressing, setShowDressing] = useState(true);
  const [zoom, setZoom] = useState(1);
  const [status, setStatus] = useState('Unsaved workshop');
  const [dragging, setDragging] = useState<{ id: string; offset: Vec2 } | null>(null);
  const [viewMode, setViewMode] = useState<'2d' | '3d'>('2d');
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
  const selectedStaff = selectedRecord?.kind === 'staff' ? document.staff.find(member => member.id === selectedRecord.id) ?? null : null;
  const selectedWaypoint = selectedRecord?.kind === 'waypoint' ? document.waypoints.find(waypoint => waypoint.id === selectedRecord.id) ?? null : null;
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
    setSelectedRecord({ kind: 'element', id: element.id });
  };

  const handleCanvasPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.target !== event.currentTarget) return;
    placeElement(activeTool, canvasPoint(event));
  };

  const handleElementPointerDown = (event: ReactPointerEvent<HTMLDivElement>, element: LevelElement) => {
    event.stopPropagation();
    const point = canvasPoint(event);
    setSelectedId(element.id);
    setSelectedRecord({ kind: 'element', id: element.id });
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
    if (selectedRecord?.kind === 'staff') {
      updateDocument(draft => { draft.staff = draft.staff.filter(member => member.id !== selectedRecord.id); });
    } else if (selectedRecord?.kind === 'waypoint') {
      updateDocument(draft => { draft.waypoints = draft.waypoints.filter(waypoint => waypoint.id !== selectedRecord.id); });
    } else if (selectedId) {
      updateDocument(draft => { draft.elements = draft.elements.filter(element => element.id !== selectedId); });
    } else return;
    setSelectedId(null);
    setSelectedRecord(null);
  };

  const addStaffAssignment = () => {
    const name = CANONICAL_STAFF.find(candidate => !document.staff.some(member => member.name === candidate)) ?? CANONICAL_STAFF[0];
    const member: StaffAssignment = { id: idFor('staff'), name, job: 'Assigned from canonical roster', department: 'General', floorId: currentFloor.id, x: Math.floor(currentFloor.width / 2), y: Math.floor(currentFloor.height / 2), homeElementId: null, waypointTags: ['Desk'], isExecutiveThreat: name === 'Mr Purple' };
    updateDocument(draft => draft.staff.push(member));
    setSelectedRecord({ kind: 'staff', id: member.id });
    setSelectedId(null);
    setStatus(`${name} assigned to ${currentFloor.name}`);
  };

  const addWaypoint = () => {
    const waypoint: WorkshopWaypoint = { id: idFor('waypoint'), floorId: currentFloor.id, label: 'New waypoint', x: Math.floor(currentFloor.width / 2), y: Math.floor(currentFloor.height / 2), tags: ['Desk'], capacity: 4, visibility: .5, socialValue: .5, coverValue: .5 };
    updateDocument(draft => draft.waypoints.push(waypoint));
    setSelectedRecord({ kind: 'waypoint', id: waypoint.id });
    setSelectedId(null);
    setStatus('Waypoint added');
  };

  const addFloorLink = () => {
    const fromFloor = currentFloor.id;
    const toFloor = document.floors.find(floor => floor.id !== fromFloor)?.id ?? fromFloor;
    const element = visibleElements.find(item => item.type === 'elevator' || item.type === 'stair');
    if (!element || fromFloor === toFloor) { setStatus('Place an elevator or stair and add another floor first'); return; }
    const link: WorkshopFloorLink = { id: idFor('link'), fromFloor, toFloor, elementId: element.id };
    updateDocument(draft => draft.floorLinks.push(link));
    setStatus('Floor link added');
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

  const saveGodot = () => {    const godot = {
      format: 'office-shift-godot-level', version: 2, business: document.business,
      floors: document.floors.map(floor => ({ ...floor, elements: document.elements.filter(element => element.floorId === floor.id), waypoints: document.waypoints.filter(waypoint => waypoint.floorId === floor.id), staff: document.staff.filter(member => member.floorId === floor.id) })),
      accessCards: document.accessCards,
      staff: document.staff,
      waypoints: document.waypoints,
      floorLinks: document.floorLinks,
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
          version: 2,
          accessCards: (Array.isArray(parsed.accessCards) ? parsed.accessCards : initialCards).map(card => ({ ...card, methods: Array.isArray(card.methods) && card.methods.length ? card.methods : ['Steal'] })),
          staff: Array.isArray(parsed.staff) ? parsed.staff : [],
          waypoints: Array.isArray(parsed.waypoints) ? parsed.waypoints : [],
          floorLinks: Array.isArray(parsed.floorLinks) ? parsed.floorLinks : [],
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

  const updateStaff = (patch: Partial<StaffAssignment>) => {
    if (!selectedStaff) return;
    updateDocument(draft => { const member = draft.staff.find(item => item.id === selectedStaff.id); if (member) Object.assign(member, patch); });
  };

  const updateWaypoint = (patch: Partial<WorkshopWaypoint>) => {
    if (!selectedWaypoint) return;
    updateDocument(draft => { const waypoint = draft.waypoints.find(item => item.id === selectedWaypoint.id); if (waypoint) Object.assign(waypoint, patch); });
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
              <button className={`toggle-button ${viewMode === '2d' ? 'on' : ''}`} onClick={() => setViewMode('2d')}><Grid3X3 size={15} /> 2D</button>
              <button className={`toggle-button ${viewMode === '3d' ? 'on' : ''}`} onClick={() => setViewMode('3d')}><Box size={15} /> 3D</button>
              <span className="meta-divider" />
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
            {viewMode === '3d' ? (
              <Suspense fallback={<div style={{ display: 'grid', placeItems: 'center', height: '100%', color: '#8a9a90' }}>Loading 3D editor…</div>}>
                <LevelEditor3D
                  elements={visibleElements}
                  activeTool={activeTool}
                  palette={palette}
                  gridWidth={currentFloor.width}
                  gridHeight={currentFloor.height}
                  onPlace={(type, x, y, _w, _h) => placeElement(type, { x: x + 1, y: y + 1 })}
                  onSelect={setSelectedId}
                  selectedId={selectedId}
                />
              </Suspense>
            ) : (
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
              {document.staff.filter(member => member.floorId === currentFloor.id).map(member => <button key={member.id} className="dressing-marker dressing-staff" style={{ left: `${((member.x - 1) / currentFloor.width) * 100}%`, top: `${((member.y - 1) / currentFloor.height) * 100}%` }} onClick={event => { event.stopPropagation(); setSelectedId(null); setSelectedRecord({ kind: 'staff', id: member.id }); }} title={`Staff: ${member.name}`}>●</button>)}
              {document.waypoints.filter(waypoint => waypoint.floorId === currentFloor.id).map(waypoint => <button key={waypoint.id} className="dressing-marker dressing-waypoint" style={{ left: `${((waypoint.x - 1) / currentFloor.width) * 100}%`, top: `${((waypoint.y - 1) / currentFloor.height) * 100}%` }} onClick={event => { event.stopPropagation(); setSelectedId(null); setSelectedRecord({ kind: 'waypoint', id: waypoint.id }); }} title={`Waypoint: ${waypoint.label}`}>+</button>)}
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
            )}
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
          {selectedStaff && <div className="inspector-fields">
            <div className="selection-summary"><span className="summary-icon palette-reception"><Users size={16} /></span><div><strong>{selectedStaff.name}</strong><small>Canonical staff assignment</small></div><button className="icon-button danger-icon" title="Delete staff assignment" onClick={deleteSelected}><Trash2 size={15} /></button></div>
            <label className="field-label">Staff member<select value={selectedStaff.name} onChange={event => updateStaff({ name: event.target.value, isExecutiveThreat: event.target.value === 'Mr Purple' })}>{CANONICAL_STAFF.map(name => <option key={name}>{name}</option>)}</select></label>
            <label className="field-label">Department<select value={selectedStaff.department} onChange={event => updateStaff({ department: event.target.value as Department })}>{DEPARTMENTS.map(department => <option key={department}>{department}</option>)}</select></label>
            <div className="field-row"><label className="field-label">Grid X<input type="number" min={1} max={currentFloor.width} value={selectedStaff.x} onChange={event => updateStaff({ x: Math.max(1, Math.min(currentFloor.width, Number(event.target.value))) })} /></label><label className="field-label">Grid Y<input type="number" min={1} max={currentFloor.height} value={selectedStaff.y} onChange={event => updateStaff({ y: Math.max(1, Math.min(currentFloor.height, Number(event.target.value))) })} /></label></div>
            <label className="field-label">Home element<select value={selectedStaff.homeElementId ?? ''} onChange={event => updateStaff({ homeElementId: event.target.value || null })}><option value="">None</option>{visibleElements.map(element => <option key={element.id} value={element.id}>{element.label}</option>)}</select></label>
            <label className="check-line"><input type="checkbox" checked={selectedStaff.isExecutiveThreat} onChange={event => updateStaff({ isExecutiveThreat: event.target.checked })} /><span>Executive threat / roaming boss</span></label>
          </div>}
          {selectedWaypoint && <div className="inspector-fields">
            <div className="selection-summary"><span className="summary-icon palette-terminal"><MousePointer2 size={16} /></span><div><strong>{selectedWaypoint.label}</strong><small>Authored NPC navigation anchor</small></div><button className="icon-button danger-icon" title="Delete waypoint" onClick={deleteSelected}><Trash2 size={15} /></button></div>
            <label className="field-label">Label<input value={selectedWaypoint.label} onChange={event => updateWaypoint({ label: event.target.value })} /></label>
            <div className="field-row"><label className="field-label">Grid X<input type="number" min={1} max={currentFloor.width} value={selectedWaypoint.x} onChange={event => updateWaypoint({ x: Math.max(1, Math.min(currentFloor.width, Number(event.target.value))) })} /></label><label className="field-label">Grid Y<input type="number" min={1} max={currentFloor.height} value={selectedWaypoint.y} onChange={event => updateWaypoint({ y: Math.max(1, Math.min(currentFloor.height, Number(event.target.value))) })} /></label></div>
            <div className="field-row"><label className="field-label">Capacity<input type="number" min={1} max={32} value={selectedWaypoint.capacity} onChange={event => updateWaypoint({ capacity: Math.max(1, Math.min(32, Number(event.target.value))) })} /></label><label className="field-label">Visibility<input type="number" min={0} max={1} step={.1} value={selectedWaypoint.visibility} onChange={event => updateWaypoint({ visibility: Math.max(0, Math.min(1, Number(event.target.value))) })} /></label></div>
            <div className="field-row"><label className="field-label">Social value<input type="number" min={0} max={1} step={.1} value={selectedWaypoint.socialValue} onChange={event => updateWaypoint({ socialValue: Math.max(0, Math.min(1, Number(event.target.value))) })} /></label><label className="field-label">Cover value<input type="number" min={0} max={1} step={.1} value={selectedWaypoint.coverValue} onChange={event => updateWaypoint({ coverValue: Math.max(0, Math.min(1, Number(event.target.value))) })} /></label></div>
            <label className="field-label">Tags (comma separated)<input value={selectedWaypoint.tags.join(', ')} onChange={event => updateWaypoint({ tags: event.target.value.split(',').map(tag => tag.trim()).filter(Boolean) })} /></label>
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

          <div className="inspector-section access-section"><div className="rail-heading"><span>Staff & waypoints</span><span className="selection-state">{document.staff.length} / {CANONICAL_STAFF.length}</span></div><div className="access-note"><Users size={14} /><span>Author canonical staff positions and navigation anchors for the imported runtime level.</span></div><div className="footer-actions"><button className="footer-button" onClick={addStaffAssignment}><Users size={14} /> Add staff</button><button className="footer-button" onClick={addWaypoint}><MousePointer2 size={14} /> Add waypoint</button><button className="footer-button" onClick={addFloorLink}><Layers3 size={14} /> Link floor</button></div></div>

          <div className="inspector-section access-section"><div className="rail-heading"><span>Access cards</span><button className="icon-button add-floor" title="Add access card" onClick={addCard}><Plus size={15} /></button></div><div className="access-list">{document.accessCards.map(card => <div className="access-card-row" key={card.id}><span className="card-chip" style={{ background: card.color }}><KeyRound size={13} /></span><div className="access-card-info"><input value={card.name} onChange={event => updateCard(card.id, { name: event.target.value })} /><small><input value={card.holder} onChange={event => updateCard(card.id, { holder: event.target.value })} /></small><div className="method-chips">{ACCESS_METHODS.map(method => <button key={method} className={`method-chip ${card.methods.includes(method) ? 'active' : ''}`} onClick={() => toggleCardMethod(card, method)}>{method}</button>)}</div></div><select value={card.level} onChange={event => updateCard(card.id, { level: Number(event.target.value) })} aria-label={`${card.name} clearance level`}><option value={1}>L1</option><option value={2}>L2</option><option value={3}>L3</option><option value={4}>L4</option></select></div>)}</div><div className="access-note"><LockKeyhole size={14} /><span>Cards are the gate. Tag a room or door, then route access through stealing, charm, impersonation, or social engineering.</span></div></div>

          <div className="inspector-section dressing-section"><div className="rail-heading"><span>Fast dressing</span><Sparkles size={15} className="gold-icon" /></div><p>Populate safe visual detail without changing collision, access, or AI navigation.</p><button className="dress-button" onClick={autoDress}><WandSparkles size={15} /> Procedural generate props</button></div>
        </aside>
      </section>

      <footer className="workshop-footer"><div className="footer-context"><span className="context-icon"><Layers3 size={14} /></span><div><strong>{document.business}</strong><span>{document.floors.length} floors <i /> {document.elements.filter(element => element.gameplay).length} gameplay elements <i /> {document.dressing.length} dressing items</span></div></div><div className="footer-actions"><button className="footer-button" onClick={() => setDocument(starterDocument())}><FileJson size={15} /> Reset template</button><button className="footer-button" onClick={saveWorkshop}><Download size={15} /> Workshop JSON</button><button className="footer-button primary" onClick={saveGodot}><Save size={15} /> Export for Godot</button></div></footer>
    </main>
  );
}
