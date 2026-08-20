import * as THREE from 'three';

/** Axis-aligned bounding box on the floor plane (X/Z). */
export interface AABB {
  minX: number;
  minZ: number;
  maxX: number;
  maxZ: number;
}

export type RoomId =
  | 'server'
  | 'printer'
  | 'break'
  | 'closet'
  | 'reception'
  | 'floor';

export interface HideSpot {
  id: string;
  name: string;
  /** Comedic verb phrase used in toasts, e.g. "shoved under the office printer" */
  action: string;
  pos: THREE.Vector3;
  capacity: number;
  occupants: string[];
}

export interface WorldRefs {
  group: THREE.Group;
  /** Solid collision boxes (walls, desks, partitions, props). */
  colliders: AABB[];
  /** Subset of geometry that blocks NPC line of sight (partitions, walls, tall props). */
  visionBlockers: AABB[];
  waypoints: Record<string, THREE.Vector3[]>;
  hideSpots: HideSpot[];
  terminalPos: THREE.Vector3;
  trolleyPos: THREE.Vector3;
  slobDeskPos: THREE.Vector3;
  guardPosts: THREE.Vector3[];
  roomAt(x: number, z: number): RoomId;
}

/** Serializable HUD snapshot pushed from the game to React. */
export interface HudState {
  started: boolean;
  paused: boolean;
  over: boolean;
  won: boolean;
  endReason: string;
  prompt: string;
  channelProgress: number; // 0..1, -1 = not channeling
  carrying: string | null;
  disguise: string | null;
  crouching: boolean;
  hasMop: boolean;
  hasBlueprint: boolean;
  blueprintSent: boolean;
  alert: boolean;
  timeLeft: number; // seconds
  maxSuspicion: number; // 0..100 highest current witness suspicion
  beingWatched: boolean;
  objectives: { label: string; done: boolean }[];
  stats: { bonks: number; hides: number; reports: number; disguises: number; cleans: number };
}

export type ToastKind = 'info' | 'warn' | 'chaos' | 'success';
