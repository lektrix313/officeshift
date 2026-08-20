import * as THREE from 'three';
import { buildWorld } from './world';
import { NPC, type Archetype } from './npc';
import { createPhysicsWorld, Ragdoll } from './ragdoll';
import type { AABB, HideSpot, HudState, ToastKind, WorldRefs } from './types';

// ---------- geometry helpers ----------

function circleVsAABBs(pos: THREE.Vector3, r: number, boxes: AABB[]) {
  for (const b of boxes) {
    const cx = Math.max(b.minX, Math.min(pos.x, b.maxX));
    const cz = Math.max(b.minZ, Math.min(pos.z, b.maxZ));
    const dx = pos.x - cx;
    const dz = pos.z - cz;
    const d2 = dx * dx + dz * dz;
    if (d2 < r * r) {
      if (d2 < 1e-8) {
        // center inside box: push out along smallest penetration axis
        const pushL = pos.x - b.minX, pushR = b.maxX - pos.x;
        const pushT = pos.z - b.minZ, pushB = b.maxZ - pos.z;
        const m = Math.min(pushL, pushR, pushT, pushB);
        if (m === pushL) pos.x = b.minX - r;
        else if (m === pushR) pos.x = b.maxX + r;
        else if (m === pushT) pos.z = b.minZ - r;
        else pos.z = b.maxZ + r;
      } else {
        const d = Math.sqrt(d2);
        pos.x = cx + (dx / d) * r;
        pos.z = cz + (dz / d) * r;
      }
    }
  }
}

/** 2D segment vs AABB intersection (slab method). */
function segHitsAABB(x1: number, z1: number, x2: number, z2: number, b: AABB): boolean {
  const dx = x2 - x1, dz = z2 - z1;
  let tmin = 0, tmax = 1;
  if (Math.abs(dx) < 1e-9) {
    if (x1 < b.minX || x1 > b.maxX) return false;
  } else {
    let t1 = (b.minX - x1) / dx, t2 = (b.maxX - x1) / dx;
    if (t1 > t2) [t1, t2] = [t2, t1];
    tmin = Math.max(tmin, t1); tmax = Math.min(tmax, t2);
    if (tmin > tmax) return false;
  }
  if (Math.abs(dz) < 1e-9) {
    if (z1 < b.minZ || z1 > b.maxZ) return false;
  } else {
    let t1 = (b.minZ - z1) / dz, t2 = (b.maxZ - z1) / dz;
    if (t1 > t2) [t1, t2] = [t2, t1];
    tmin = Math.max(tmin, t1); tmax = Math.min(tmax, t2);
    if (tmin > tmax) return false;
  }
  return true;
}

// ---------- audio (tiny synth, all vibes, no assets) ----------

class BlipSynth {
  private ctx: AudioContext | null = null;
  private ensure() {
    if (!this.ctx) {
      try { this.ctx = new AudioContext(); } catch { /* no audio, no problem */ }
    }
    return this.ctx;
  }
  blip(freq: number, dur = 0.12, type: OscillatorType = 'square', vol = 0.06) {
    const ctx = this.ensure();
    if (!ctx) return;
    try {
      const o = ctx.createOscillator();
      const g = ctx.createGain();
      o.type = type;
      o.frequency.value = freq;
      g.gain.setValueAtTime(vol, ctx.currentTime);
      g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + dur);
      o.connect(g).connect(ctx.destination);
      o.start();
      o.stop(ctx.currentTime + dur);
    } catch { /* ignore */ }
  }
  bonk() { this.blip(180, 0.15, 'square', 0.09); this.blip(90, 0.25, 'sawtooth', 0.05); }
  alarm() { this.blip(660, 0.3, 'square', 0.05); setTimeout(() => this.blip(520, 0.3, 'square', 0.05), 200); }
  success() { [523, 659, 784].forEach((f, i) => setTimeout(() => this.blip(f, 0.18, 'triangle', 0.07), i * 110)); }
  pickup() { this.blip(440, 0.08, 'triangle', 0.06); }
}

// ---------- the game ----------

interface CoworkerDef { name: string; archetype: Archetype; pos: [number, number]; zone: string; }

const COWORKERS: CoworkerDef[] = [
  { name: 'Keith', archetype: 'snoop', pos: [0, -2], zone: 'snoop' },
  { name: 'Susan', archetype: 'gossip', pos: [10, 4], zone: 'gossip' },
  { name: 'Dave', archetype: 'slob', pos: [-16.2, 0], zone: 'drone' },
  { name: 'Tom', archetype: 'grifter', pos: [24, 11], zone: 'grifter' },
  { name: 'Greg', archetype: 'drone', pos: [-10, 4], zone: 'drone' },
  { name: 'Janet', archetype: 'drone', pos: [5, -9], zone: 'drone' },
  { name: 'Priya', archetype: 'drone', pos: [-20, -9], zone: 'drone' },
  { name: 'Margaret', archetype: 'drone', pos: [16, -12], zone: 'drone' },
  { name: 'Linda', archetype: 'drone', pos: [0, 11], zone: 'drone' },
  { name: 'Barry', archetype: 'drone', pos: [-26, -5], zone: 'gossip' },
];

const SHIFT_SECONDS = 360;
const INTERACT_RANGE = 2.4;
const BONK_RANGE = 2.1;
const BONK_COOLDOWN = 0.8;
const CHANNEL_TIME = 3.5;
const MOP_TIME = 2.2;
const PHOTO_COOLDOWN = 30;

const AMBIENT_LINES = [
  '{name} is microwaving fish in the break room. The audacity. 🐟',
  '{name} just said "circle back" eleven times in one sentence. 🔄',
  '{name} is rage-typing an email about the thermostat. 🌡️',
  '{name} has been "in a meeting" for three hours. The meeting is Candy Crush. 🍬',
  '{name} labeled their yogurt. It is plain yogurt. Nobody wants it. 🥛',
  'Someone ate {name}\'s lunch. HR is "looking into it". 🥪',
  '{name} is explaining blockchain to the vending machine. 🪙',
  '{name} scheduled a meeting about there being too many meetings. 📅',
  '{name} printed 200 pages of a PDF they will never read. 🖨️',
  'The office plant died. {name} is holding a small funeral. 🪴',
  '{name} is chewing loudly. Morale has never been lower. 🥨',
  '{name} just replied-all to the entire company. Chaos reigns. 📧',
  '{name} put "synergy" on the whiteboard and underlined it twice. 📉',
  '{name} is watching a tutorial on how to look busy. 📺',
];

interface BloodSplat {
  mesh: THREE.Mesh;
  pos: THREE.Vector3;
}

function makeBloodTexture(): THREE.Texture {
  const canvas = document.createElement('canvas');
  canvas.width = 128;
  canvas.height = 128;
  const ctx = canvas.getContext('2d')!;
  for (let i = 0; i < 9; i++) {
    const x = 34 + Math.random() * 60;
    const y = 34 + Math.random() * 60;
    const r = 8 + Math.random() * 22;
    const g = ctx.createRadialGradient(x, y, 1, x, y, r);
    g.addColorStop(0, 'rgba(140, 10, 10, 0.95)');
    g.addColorStop(0.7, 'rgba(110, 8, 8, 0.8)');
    g.addColorStop(1, 'rgba(90, 5, 5, 0)');
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
  }
  const tex = new THREE.CanvasTexture(canvas);
  return tex;
}

export class Game {
  private renderer!: THREE.WebGLRenderer;
  private scene = new THREE.Scene();
  private camera!: THREE.PerspectiveCamera;
  private world!: WorldRefs;
  private npcs: NPC[] = [];
  private guard: NPC | null = null;
  private clock = new THREE.Clock();
  private synth = new BlipSynth();
  private phys = createPhysicsWorld();

  // blood evidence
  private blood: BloodSplat[] = [];
  private bloodTex = makeBloodTexture();
  private lastDripPos = new THREE.Vector3();

  // player
  private playerPos = new THREE.Vector3(0, 0, 18.5);
  private yaw = 0; // face north (-Z), into the cubicle farm
  private pitch = 0;
  private keys = new Set<string>();
  private crouching = false;
  private carrying: NPC | null = null;
  private carriedMesh: THREE.Group | null = null;
  private disguiseOf: string | null = null;
  private bonkTimer = 0;
  private channelT = -1;
  private channelMode: 'terminal' | 'mop' = 'terminal';
  private channelSplat: BloodSplat | null = null;
  private hasBlueprint = false;
  private blueprintSent = false;
  private hasMop = false;

  // meta
  private started = false;
  private over = false;
  private won = false;
  private endReason = '';
  private timeLeft = SHIFT_SECONDS;
  private alertTimer = 0; // >0 while security is hunting
  private stats = { bonks: 0, hides: 0, reports: 0, disguises: 0, cleans: 0 };
  private beingWatched = false;
  private prompt = '';
  private hudTimer = 0;
  private lockFailed = false;
  private disposed = false;
  private chatterTimer = 9;
  private photoCooldown = 0;

  onHud: (h: HudState) => void = () => {};
  onToast: (msg: string, kind: ToastKind) => void = () => {};

  private container: HTMLElement;

  constructor(container: HTMLElement) {
    this.container = container;
  }

  // ================= setup =================

  init() {
    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setSize(this.container.clientWidth, this.container.clientHeight);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.container.appendChild(this.renderer.domElement);

    this.scene.background = new THREE.Color(0x0e1420);
    this.scene.fog = new THREE.Fog(0x0e1420, 30, 70);

    this.camera = new THREE.PerspectiveCamera(72, this.container.clientWidth / this.container.clientHeight, 0.1, 120);
    this.scene.add(this.camera); // carried-body bundle is parented to the camera

    // lighting: fluorescent office vibes
    const ambient = new THREE.AmbientLight(0xcfd8ff, 0.55);
    this.scene.add(ambient);
    const sun = new THREE.DirectionalLight(0xfff2dd, 0.9);
    sun.position.set(18, 24, 10);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.camera.left = -40; sun.shadow.camera.right = 40;
    sun.shadow.camera.top = 40; sun.shadow.camera.bottom = -40;
    this.scene.add(sun);

    this.world = buildWorld(this.scene);

    // spawn coworkers once the rigged model is ready (fallback: primitives)
    NPC.loadAssets().then(() => this.spawnCharacters());

    // events
    window.addEventListener('resize', this.onResize);
    document.addEventListener('keydown', this.onKeyDown);
    document.addEventListener('keyup', this.onKeyUp);
    document.addEventListener('mousemove', this.onMouseMove);
    document.addEventListener('pointerlockchange', this.onLockChange);
    this.renderer.domElement.addEventListener('click', () => {
      if (this.started && !this.over && !this.isLocked()) this.lock();
    });

    this.pushHud();
    this.loop();
  }

  private spawnCharacters() {
    for (const def of COWORKERS) {
      const npc = new NPC(def.name, def.archetype, new THREE.Vector3(def.pos[0], 0, def.pos[1]));
      (npc as NPC & { zone: string }).zone = def.zone;
      this.npcs.push(npc);
      this.scene.add(npc.group);
    }
    // the slob is slumped at his desk
    const dave = this.npcs.find(n => n.archetype === 'slob')!;
    dave.pos.copy(this.world.slobDeskPos);

    // security
    const guard = new NPC('Briggs', 'guard', this.world.guardPosts[0].clone());
    (guard as NPC & { zone: string }).zone = 'guard';
    guard.pauseTimer = 2;
    this.guard = guard;
    this.npcs.push(guard);
    this.scene.add(guard.group);
  }

  dispose() {
    this.disposed = true;
    window.removeEventListener('resize', this.onResize);
    document.removeEventListener('keydown', this.onKeyDown);
    document.removeEventListener('keyup', this.onKeyUp);
    document.removeEventListener('mousemove', this.onMouseMove);
    document.removeEventListener('pointerlockchange', this.onLockChange);
    this.renderer.dispose();
    this.container.removeChild(this.renderer.domElement);
  }

  start() {
    this.started = true;
    this.lock();
    this.pushHud();
  }

  /** Re-engage mouse control (called from the pause overlay). */
  resume() {
    if (!this.over) this.lock();
    this.pushHud();
  }

  private lock() {
    try {
      const p = this.renderer.domElement.requestPointerLock() as unknown as Promise<void> | undefined;
      if (p && typeof p.catch === 'function') {
        p.catch(() => {
          // Pointer lock blocked (e.g. sandboxed preview iframe) — fall back to plain mouse-look.
          this.lockFailed = true;
          this.pushHud();
        });
      }
    } catch {
      this.lockFailed = true;
      this.pushHud();
    }
  }

  private isLocked(): boolean {
    return document.pointerLockElement === this.renderer.domElement;
  }

  /** True when the player has mouse control (pointer lock, or fallback mode). */
  private hasMouseControl(): boolean {
    return this.isLocked() || this.lockFailed;
  }

  private onResize = () => {
    this.camera.aspect = this.container.clientWidth / this.container.clientHeight;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(this.container.clientWidth, this.container.clientHeight);
  };

  private onLockChange = () => this.pushHud();

  private onMouseMove = (e: MouseEvent) => {
    if (this.over || !this.hasMouseControl()) return;
    this.yaw -= e.movementX * 0.0023;
    this.pitch = Math.max(-1.4, Math.min(1.4, this.pitch - e.movementY * 0.0023));
  };

  private onKeyDown = (e: KeyboardEvent) => {
    if (e.repeat) return;
    const k = e.key.toLowerCase();
    this.keys.add(k);
    if (!this.started || this.over || !this.hasMouseControl()) return;
    if (k === 'c') {
      this.crouching = !this.crouching;
      this.pushHud();
    }
    if (k === 'f') this.tryBonk();
    if (k === 'q') this.toggleCarry();
  };

  private onKeyUp = (e: KeyboardEvent) => {
    this.keys.delete(e.key.toLowerCase());
  };

  // ================= helpers =================

  private losBlocked(a: THREE.Vector3, b: THREE.Vector3): boolean {
    for (const w of this.world.visionBlockers) {
      if (segHitsAABB(a.x, a.z, b.x, b.z, w)) return true;
    }
    return false;
  }

  private canSee(npc: NPC, target: THREE.Vector3, rangeMul = 1): boolean {
    if (!npc.awake || npc.state === 'seated') return false;
    const range = npc.spec.range * rangeMul * (this.crouching ? 0.65 : 1);
    const dx = target.x - npc.pos.x;
    const dz = target.z - npc.pos.z;
    const dist = Math.hypot(dx, dz);
    if (dist > range) return false;
    const ang = Math.atan2(dx, dz);
    let diff = ang - npc.facing;
    while (diff > Math.PI) diff -= Math.PI * 2;
    while (diff < -Math.PI) diff += Math.PI * 2;
    if (Math.abs(diff) > npc.spec.fov) return false;
    return !this.losBlocked(npc.pos, target);
  }

  /** Camera forward on the floor plane (Three.js cameras look down -Z). */
  private forwardVec(): THREE.Vector3 {
    return new THREE.Vector3(-Math.sin(this.yaw), 0, -Math.cos(this.yaw));
  }

  private toast(msg: string, kind: ToastKind = 'info') {
    this.onToast(msg, kind);
  }

  // ================= blood evidence =================

  private spawnBlood(pos: THREE.Vector3, count: number, maxSize: number) {
    for (let i = 0; i < count; i++) {
      const size = maxSize * (0.5 + Math.random() * 0.5);
      const geo = new THREE.PlaneGeometry(size, size);
      const mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({
        map: this.bloodTex,
        transparent: true,
        depthWrite: false,
      }));
      mesh.rotation.x = -Math.PI / 2;
      mesh.rotation.z = Math.random() * Math.PI * 2;
      const off = new THREE.Vector3((Math.random() - 0.5) * 0.9, 0, (Math.random() - 0.5) * 0.9);
      mesh.position.set(pos.x + off.x, 0.021 + Math.random() * 0.004, pos.z + off.z);
      mesh.renderOrder = 1;
      this.scene.add(mesh);
      this.blood.push({ mesh, pos: mesh.position.clone() });
    }
    // cap the crime scene
    while (this.blood.length > 90) {
      const old = this.blood.shift()!;
      this.scene.remove(old.mesh);
    }
  }

  private removeSplat(splat: BloodSplat) {
    this.scene.remove(splat.mesh);
    this.blood = this.blood.filter(b => b !== splat);
    // anyone investigating this exact stain loses the plot
    for (const n of this.npcs) {
      if ((n.state === 'curious' || n.state === 'panic') && (n.investigateRef === splat || n.panicRef === splat)) {
        n.shrugItOff();
        n.suspicion = Math.max(n.suspicion, 35);
        this.toast(`${n.name} blinks. The stain is gone. "I need more coffee." ☕`, 'info');
      }
    }
  }

  private nearestSplat(range: number): BloodSplat | null {
    let best: BloodSplat | null = null;
    let bestDist = range;
    for (const b of this.blood) {
      const d = Math.hypot(b.pos.x - this.playerPos.x, b.pos.z - this.playerPos.z);
      if (d < bestDist) { best = b; bestDist = d; }
    }
    return best;
  }

  /** Remove any curiosity/panic attached to a now-hidden body. */
  private clearInvestigationsOf(npc: NPC, hiddenIn: string) {
    for (const n of this.npcs) {
      if ((n.state === 'curious' || n.state === 'panic') && (n.investigateRef === npc || n.panicRef === npc)) {
        n.shrugItOff();
        n.suspicion = Math.max(n.suspicion, 40);
        this.toast(`${n.name} saw ${npc.name} vanish into the ${hiddenIn}. "…Nope. Not paid enough." 🫠`, 'warn');
      }
    }
  }

  private equipMop() {
    this.hasMop = true;
    const mop = new THREE.Group();
    const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.015, 0.015, 0.7, 6), new THREE.MeshStandardMaterial({ color: 0x8a6f4f }));
    handle.rotation.z = 0.5;
    mop.add(handle);
    const headMesh = new THREE.Mesh(new THREE.SphereGeometry(0.09, 8, 6), new THREE.MeshStandardMaterial({ color: 0xd8d8d0, roughness: 1 }));
    headMesh.position.set(-0.28, -0.32, 0);
    headMesh.scale.y = 0.6;
    mop.add(headMesh);
    mop.position.set(-0.42, -0.35, -0.8);
    this.camera.add(mop); // FPS-style mop viewmodel, forever
    this.synth.pickup();
    this.toast('Mop acquired. You have never looked more employable. 🧹', 'success');
  }

  // ================= actions =================

  private tryBonk() {
    if (this.bonkTimer > 0 || this.carrying || this.channelT >= 0) return;
    const fwd = this.forwardVec();
    let best: NPC | null = null;
    let bestDist = BONK_RANGE;
    for (const n of this.npcs) {
      if (!n.awake) continue;
      const d = Math.hypot(n.pos.x - this.playerPos.x, n.pos.z - this.playerPos.z);
      if (d > bestDist) continue;
      const dir = new THREE.Vector3(n.pos.x - this.playerPos.x, 0, n.pos.z - this.playerPos.z).normalize();
      if (dir.dot(fwd) < 0.35) continue;
      best = n;
      bestDist = d;
    }
    if (!best) return;
    this.bonkTimer = BONK_COOLDOWN;
    this.synth.bonk();
    this.stats.bonks++;

    const victim = best;
    const wasAsleep = victim.state === 'seated';
    const flopDir = new THREE.Vector3(victim.pos.x - this.playerPos.x, 0, victim.pos.z - this.playerPos.z).normalize();
    victim.knockOut(this.phys, flopDir);
    this.spawnBlood(victim.pos, 4, 1.1);
    this.toast(wasAsleep
      ? `${victim.name} was already asleep. You just made it official. And messy. 🩸💤`
      : `You bonked ${victim.name} with a keyboard. There is… some blood. Mop's in the supply closet. ⌨️🩸`, 'chaos');

    // witnesses?
    for (const w of this.npcs) {
      if (w === victim || !w.awake) continue;
      const dist = Math.hypot(w.pos.x - this.playerPos.x, w.pos.z - this.playerPos.z);
      const seen = this.canSee(w, this.playerPos) || dist < 4;
      if (seen) {
        if (w.state === 'seated' && dist < 5) {
          w.state = 'routine'; // the noise wakes the slob
          if (w.zzz) { w.group.remove(w.zzz); w.zzz = null; }
          w.addSuspicion(100);
          this.toast(`The commotion woke ${w.name} up — and they saw EVERYTHING. 👀`, 'warn');
        } else {
          w.addSuspicion(70);
          this.toast(`${w.name} saw that. ${w.name} is reconsidering your friendship. 😨`, 'warn');
        }
        if (this.disguiseOf) this.blowDisguise();
      }
    }
  }

  private toggleCarry() {
    if (this.channelT >= 0) return;
    if (this.carrying) {
      // drop the body — it flops again, because physics has a sense of humour
      const n = this.carrying;
      const fwd = this.forwardVec();
      n.pos.set(this.playerPos.x + fwd.x * 1.1, 0, this.playerPos.z + fwd.z * 1.1);
      circleVsAABBs(n.pos, 0.3, this.world.colliders);
      n.group.visible = true;
      if (NPC.modelLoaded) {
        n.ragdoll = new Ragdoll(this.phys, n.group, fwd.clone().multiplyScalar(0.6));
        if (!n.ragdoll.active) n.ragdoll = null;
      }
      this.spawnBlood(n.pos, 1, 0.7);
      this.carrying = null;
      if (this.carriedMesh) { this.camera.remove(this.carriedMesh); this.carriedMesh = null; }
      this.synth.pickup();
      this.toast(`You set ${n.name} down. Gently-ish. They're leaking a bit.`, 'info');
      return;
    }
    // pick up nearest body
    let best: NPC | null = null;
    let bestDist = INTERACT_RANGE;
    for (const n of this.npcs) {
      if (n.state !== 'out') continue;
      const d = Math.hypot(n.pos.x - this.playerPos.x, n.pos.z - this.playerPos.z);
      if (d < bestDist) { best = n; bestDist = d; }
    }
    if (!best) return;
    this.carrying = best;
    best.clearRagdoll();
    best.group.visible = false;
    this.lastDripPos.copy(this.playerPos);
    // build the carried-body visual (a very dignified bundle)
    const bundle = new THREE.Group();
    const torso = new THREE.Mesh(new THREE.CapsuleGeometry(0.3, 0.9, 6, 10), new THREE.MeshStandardMaterial({ color: best.spec.color, roughness: 0.9 }));
    torso.rotation.z = Math.PI / 2;
    bundle.add(torso);
    const head = new THREE.Mesh(new THREE.SphereGeometry(0.2, 10, 8), new THREE.MeshStandardMaterial({ color: 0xe8b98a }));
    head.position.set(0.65, 0.05, 0);
    bundle.add(head);
    bundle.position.set(0.35, -0.55, -1.0);
    bundle.rotation.z = 0.2;
    this.camera.add(bundle);
    this.carriedMesh = bundle;
    this.synth.pickup();
    this.toast(`You are now carrying ${best.name}. Act natural. Mind the drips. 🧍🩸`, 'warn');
  }

  private blowDisguise() {
    if (!this.disguiseOf) return;
    this.toast(`Your ${this.disguiseOf} disguise is BLOWN. You are you again. 🎭`, 'warn');
    this.disguiseOf = null;
  }

  private hideBodyIn(spot: HideSpot) {
    const n = this.carrying;
    if (!n) return;
    spot.occupants.push(n.name);
    n.state = 'hidden';
    n.clearRagdoll();
    n.group.visible = false;
    this.carrying = null;
    if (this.carriedMesh) { this.camera.remove(this.carriedMesh); this.carriedMesh = null; }
    this.stats.hides++;
    this.synth.pickup();
    this.clearInvestigationsOf(n, spot.name);
    this.toast(`${n.name} was ${spot.action}. No one will ever look there.`, 'success');

    // comedy gags
    if (spot.id === 'lamp') {
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 1.7, 8), new THREE.MeshStandardMaterial({ color: 0x3a3f47 }));
      pole.position.set(spot.pos.x, 0.85, spot.pos.z);
      const shade = new THREE.Mesh(new THREE.ConeGeometry(0.55, 0.7, 12, 1, true), new THREE.MeshStandardMaterial({ color: 0xf2e3b3, side: THREE.DoubleSide }));
      shade.position.set(spot.pos.x, 1.85, spot.pos.z);
      const tie = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.5, 0.05), new THREE.MeshStandardMaterial({ color: n.spec.color }));
      tie.position.set(spot.pos.x, 1.2, spot.pos.z + 0.1);
      this.scene.add(pole, shade, tie);
    } else if (spot.id === 'printer') {
      for (const s of [-0.2, 0.2]) {
        const shoe = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.14, 0.45), new THREE.MeshStandardMaterial({ color: 0x2a2a2e }));
        shoe.position.set(spot.pos.x + s, 0.08, spot.pos.z + 0.9);
        this.scene.add(shoe);
      }
    } else if (spot.id === 'trolley') {
      const lump = new THREE.Mesh(new THREE.SphereGeometry(0.55, 10, 8), new THREE.MeshStandardMaterial({ color: 0xcbb791, roughness: 1 }));
      lump.scale.set(1, 0.6, 1.4);
      lump.position.set(spot.pos.x, 1.15, spot.pos.z);
      this.scene.add(lump);
    }
  }

  private deliverBlueprint() {
    this.hasBlueprint = false;
    this.blueprintSent = true;
    this.synth.success();
    this.toast('Blueprints mailed to "definitely not a rival company". 📮✅', 'success');
    this.endGame(true, 'Blueprint delivered. OmniCore never stood a chance.');
  }

  // ================= per-frame =================

  private loop = () => {
    if (this.disposed) return;
    requestAnimationFrame(this.loop);
    const dt = Math.min(this.clock.getDelta(), 0.05);

    if (this.started && !this.over && this.hasMouseControl()) {
      this.update(dt);
    }
    // animation + labels always tick
    for (const n of this.npcs) {
      n.mixer?.update(dt);
      n.setMoving(n.moving, n.state === 'report' || n.state === 'hunt');
      n.updateVisuals(this.camera);
    }

    // camera
    const eyeH = this.crouching ? 1.0 : 1.6;
    this.camera.position.set(this.playerPos.x, eyeH, this.playerPos.z);
    this.camera.rotation.set(0, 0, 0, 'YXZ');
    this.camera.rotation.y = this.yaw;
    this.camera.rotation.x = this.pitch;

    this.renderer.render(this.scene, this.camera);

    this.hudTimer -= dt;
    if (this.hudTimer <= 0) {
      this.hudTimer = 0.12;
      this.pushHud();
    }
  };

  private update(dt: number) {
    this.timeLeft -= dt;
    this.bonkTimer = Math.max(0, this.bonkTimer - dt);
    if (this.alertTimer > 0) this.alertTimer -= dt;
    if (this.timeLeft <= 0) {
      this.endGame(false, "The courier left without your package. Shift wasted. You're fired — from a job you never even had.");
      return;
    }

    this.updatePlayer(dt);
    this.updateNpcs(dt);
    this.updateGuard(dt);
    this.updateInteractions(dt);
    this.updateChatter(dt);

    // physics: ragdolls flop, settle, then bleed out a pool
    this.phys.step(1 / 60, dt, 3);
    for (const n of this.npcs) {
      if (n.ragdoll && !n.ragdoll.update(dt)) {
        n.ragdoll = null;
        if (!n.poolSpawned && n.state === 'out') {
          n.poolSpawned = true;
          this.spawnBlood(n.pos, 3, 1.5);
        }
      }
    }

    // carrying a bleeding body leaves a trail
    if (this.carrying && this.playerPos.distanceTo(this.lastDripPos) > 0.9) {
      this.lastDripPos.copy(this.playerPos);
      this.spawnBlood(this.playerPos, 1, 0.45);
    }
  }

  /** Ambient office nonsense so the place feels alive (and unserious). */
  private updateChatter(dt: number) {
    this.photoCooldown = Math.max(0, this.photoCooldown - dt);
    this.chatterTimer -= dt;
    if (this.chatterTimer > 0) return;
    this.chatterTimer = 11 + Math.random() * 7;
    const candidates = this.npcs.filter(n => n.awake && n !== this.guard);
    if (candidates.length === 0) return;
    const who = candidates[Math.floor(Math.random() * candidates.length)];
    const line = AMBIENT_LINES[Math.floor(Math.random() * AMBIENT_LINES.length)];
    this.toast(line.replaceAll('{name}', who.name), 'info');
  }

  /** Photocopy your face. The floor stops working to laugh at it. */
  private photocopyFace() {
    this.photoCooldown = PHOTO_COOLDOWN;
    this.synth.pickup();
    let count = 0;
    for (const n of this.npcs) {
      if (!n.awake || n.state === 'report' || n.state === 'seated') continue;
      const d = Math.hypot(n.pos.x - this.playerPos.x, n.pos.z - this.playerPos.z);
      if (d < 12) {
        n.distractTimer = 7;
        n.showEmote('😂');
        count++;
      }
    }
    this.toast(`You photocopy your face. 50 copies. ${count > 0 ? `${count} coworker${count > 1 ? 's' : ''} completely lose${count > 1 ? '' : 's'} it. 📠😂` : 'No one is around to appreciate it. Tragic. 📠'}`, 'chaos');
  }

  private updatePlayer(dt: number) {
    const speed = this.carrying ? 2.9 : this.crouching ? 2.2 : 4.6;
    const fwd = this.forwardVec();
    const right = new THREE.Vector3(-fwd.z, 0, fwd.x);
    const move = new THREE.Vector3();
    if (this.keys.has('w')) move.add(fwd);
    if (this.keys.has('s')) move.sub(fwd);
    if (this.keys.has('a')) move.sub(right);
    if (this.keys.has('d')) move.add(right);
    if (move.lengthSq() > 0) {
      move.normalize().multiplyScalar(speed * dt);
      this.playerPos.add(move);
      circleVsAABBs(this.playerPos, 0.35, this.world.colliders);
      this.playerPos.x = Math.max(-31.4, Math.min(31.4, this.playerPos.x));
      this.playerPos.z = Math.max(-21.4, Math.min(21.4, this.playerPos.z));
    }
  }

  private playerSusActivity(): number {
    // how suspicious the player currently looks: 0 = model employee
    let sus = 0;
    const room = this.world.roomAt(this.playerPos.x, this.playerPos.z);
    if (room === 'server' && !this.disguiseOf) sus = Math.max(sus, 1);
    if (this.channelT >= 0 && this.channelMode === 'terminal') sus = Math.max(sus, 2.5);
    if (this.channelT >= 0 && this.channelMode === 'mop') sus = Math.max(sus, 1.5); // mopping WHAT exactly?
    if (this.carrying) sus = Math.max(sus, 3);
    return sus;
  }

  private updateNpcs(dt: number) {
    const activity = this.playerSusActivity();
    const disguiseMul = this.disguiseOf ? 0.3 : 1;
    let maxSus = 0;
    let watched = false;

    for (const n of this.npcs) {
      if (n === this.guard || !n.awake) continue;

      // --- laughing at your photocopied face (distraction) ---
      if (n.distractTimer > 0 && n.state !== 'report') {
        n.distractTimer -= dt;
        if (n.distractTimer <= 0) n.clearEmote();
        continue; // too busy laughing to notice anything
      }

      // --- curiosity: walking over to inspect evidence ---
      if (n.state === 'curious') {
        const gone = n.investigateKind === 'body'
          ? !n.investigateRef || (n.investigateRef as NPC).state !== 'out' || !(n.investigateRef as NPC).group.visible
          : !n.investigateRef || !this.blood.includes(n.investigateRef as BloodSplat);
        if (gone) {
          n.shrugItOff();
          n.addSuspicion(12);
          this.toast(`${n.name}: "Huh. Could've sworn I saw… something." 🤨`, 'info');
        } else if (n.investigateTarget && n.stepToward(n.investigateTarget, dt, n.spec.speed * 0.9)) {
          circleVsAABBs(n.pos, 0.3, this.world.colliders);
          // arrived — full panic with a countdown until they squeal
          const kind = n.investigateKind!;
          const duration = kind === 'body' ? 8 : 4.5;
          n.startPanic(kind, n.investigateRef!, duration);
          if (kind === 'body') {
            const victim = n.investigateRef as NPC;
            this.toast(`${n.name} found ${victim.name}'s body!! 😱 Security in ${Math.round(duration)}s — unless someone stops them.`, 'chaos');
          } else {
            this.toast(`${n.name} is staring at a pool of blood. 🩸 Squealing in ${Math.round(duration)}s — mop it or stop them.`, 'warn');
          }
          this.synth.alarm();
        } else {
          circleVsAABBs(n.pos, 0.3, this.world.colliders);
        }
        maxSus = Math.max(maxSus, n.suspicion);
        continue; // focused on the evidence, not on you
      }

      // --- panic: countdown until they run to security ---
      if (n.state === 'panic') {
        const gone = n.panicKind === 'body'
          ? !n.panicRef || (n.panicRef as NPC).state !== 'out' || !(n.panicRef as NPC).group.visible
          : !n.panicRef || !this.blood.includes(n.panicRef as BloodSplat);
        if (gone) {
          n.shrugItOff();
          n.suspicion = Math.max(n.suspicion, 40);
          this.toast(`${n.name} looks again — nothing there. "…Not paid enough for this." 🫠`, 'info');
        } else if (n.updatePanic(dt)) {
          if (n.archetype === 'grifter') {
            n.shrugItOff();
            n.suspicion = 20;
            this.toast(`${n.name} saw everything… and wants a cut. You gained an accomplice 🤝`, 'success');
          } else if (n.spec.reports) {
            this.startReport(n);
          }
        }
        maxSus = Math.max(maxSus, n.suspicion);
        continue;
      }

      // --- movement / behaviour ---
      if (n.state === 'report') {
        if (n.reportTarget && n.stepToward(n.reportTarget, dt, n.spec.speed * 1.7)) {
          this.onReport(n);
        }
      } else if (n.state === 'seated') {
        // sweet dreams
      } else if (n.state === 'routine') {
        if (n.pauseTimer > 0) {
          n.pauseTimer -= dt;
          n.moving = false;
        } else {
          if (!n.target || n.stepToward(n.target, dt)) {
            const zone = (n as NPC & { zone?: string }).zone ?? 'drone';
            const pts = this.world.waypoints[zone] ?? this.world.waypoints.drone;
            n.target = pts[Math.floor(Math.random() * pts.length)].clone();
            n.pauseTimer = 1.5 + Math.random() * 4;
          }
        }
        // collision against world + other npcs-lite
        circleVsAABBs(n.pos, 0.3, this.world.colliders);
      }

      // --- perception ---
      if (n.state === 'seated') continue;
      const seesPlayer = this.canSee(n, this.playerPos);
      const playerDist = Math.hypot(n.pos.x - this.playerPos.x, n.pos.z - this.playerPos.z);
      if (seesPlayer && activity > 0) {
        watched = true;
        n.addSuspicion(activity * n.spec.rate * 22 * disguiseMul * dt);
        n.lastSeenPlayer.copy(this.playerPos);
        if (this.carrying && this.disguiseOf) this.blowDisguise();
      } else if (seesPlayer && activity === 0) {
        // low-level weirdo detection: you don't have to commit crimes to be off-putting
        if (playerDist < 1.6) {
          n.addSuspicion(9 * n.spec.rate * disguiseMul * dt);
          if (!n.creepToastDone && n.suspicion > 15) {
            n.creepToastDone = true;
            this.toast(`${n.name}: "Do I… know you? You're standing VERY close." 😐`, 'warn');
          }
        } else if (this.crouching && playerDist < 8) {
          n.addSuspicion(5 * n.spec.rate * disguiseMul * dt);
          if (!n.crabToastDone && n.suspicion > 12) {
            n.crabToastDone = true;
            this.toast(`${n.name} is wondering why you're crab-walking past the cubicles. 🦀`, 'warn');
          }
        }
      } else if (n.suspicion > 0 && n.suspicion < 50) {
        n.suspicion = Math.max(0, n.suspicion - 2.5 * dt);
      }

      // --- noticing evidence from afar: curiosity first, panic on arrival ---
      if (n.state === 'routine') {
        let spotted = false;
        for (const b of this.npcs) {
          if (b === n || b.state !== 'out' || !b.group.visible) continue;
          if (this.canSee(n, b.pos)) {
            n.startCurious(b.pos.clone(), b, 'body');
            this.toast(`${n.name} spotted something person-shaped on the floor. "…Hello?" ❓`, 'warn');
            spotted = true;
            break;
          }
        }
        if (!spotted) {
          for (const s of this.blood) {
            if (this.canSee(n, s.pos, 0.8)) {
              n.startCurious(s.pos.clone(), s, 'blood');
              this.toast(`${n.name} noticed a stain. "Is that… ketchup?" ❓`, 'info');
              break;
            }
          }
        }
      }

      // gossip spreads the word
      if (n.archetype === 'gossip' && !n.gossipSpreadDone && n.suspicion >= 60) {
        n.gossipSpreadDone = true;
        let count = 0;
        for (const o of this.npcs) {
          if (o === n || !o.awake || o === this.guard) continue;
          const d = Math.hypot(o.pos.x - n.pos.x, o.pos.z - n.pos.z);
          if (d < 9) { o.addSuspicion(30); count++; }
        }
        this.toast(`${n.name} is telling EVERYONE. (${count} coworkers looped in) 🗣️`, 'warn');
      }

      // suspicion climax
      if (n.suspicion >= 100 && n.state === 'routine') {
        if (n.archetype === 'grifter') {
          n.calmDown();
          n.suspicion = 20;
          this.toast(`${n.name} saw everything… and wants in. You gained an accomplice 🤝`, 'success');
        } else if (n.spec.reports) {
          this.startReport(n);
        }
      }

      maxSus = Math.max(maxSus, n.suspicion);
    }

    this.beingWatched = watched;
    this.maxSuspicionValue = maxSus;
  }

  private maxSuspicionValue = 0;

  private startReport(n: NPC) {
    if (!this.guard || !this.guard.awake) {
      this.toast(`${n.name} ran to tell security… but security is currently a floor lamp. 💡`, 'chaos');
      n.calmDown();
      return;
    }
    n.startReport(this.guard.pos);
    this.toast(`${n.name} is RUNNING to security! Intercept or improvise! 🏃`, 'warn');
    this.synth.alarm();
  }

  private onReport(n: NPC) {
    n.calmDown();
    this.stats.reports++;
    this.alertTimer = 20;
    if (this.guard) {
      this.guard.state = 'hunt';
      this.guard.lastSeenPlayer.copy(this.playerPos);
      this.guard.lostSightTimer = 0;
    }
    this.toast(`Officer Briggs has been informed. He is walking over with intent. 🚨`, 'warn');
    this.synth.alarm();
  }

  private updateGuard(dt: number) {
    const g = this.guard;
    if (!g || !g.awake) return;

    // even Briggs cannot resist the photocopied face
    if (g.distractTimer > 0 && g.state !== 'hunt') {
      g.distractTimer -= dt;
      if (g.distractTimer <= 0) g.clearEmote();
      return;
    }

    if (g.state === 'hunt') {
      const sees = this.canSee(g, this.playerPos, 1.15);
      if (sees) {
        g.lastSeenPlayer.copy(this.playerPos);
        g.lostSightTimer = 0;
        // personally witnessing carrying a body refreshes the hunt
        if (this.carrying) this.alertTimer = Math.max(this.alertTimer, 8);
      } else {
        g.lostSightTimer += dt;
      }
      const arrived = g.stepToward(g.lastSeenPlayer, dt, g.spec.speed);
      circleVsAABBs(g.pos, 0.35, this.world.colliders);

      const dist = Math.hypot(g.pos.x - this.playerPos.x, g.pos.z - this.playerPos.z);
      if (dist < 1.5) {
        this.endGame(false, 'Officer Briggs caught you red-handed. HR would like a word. Several words. In a basement.');
        return;
      }
      if ((arrived && g.lostSightTimer > 4) || g.lostSightTimer > 6 || this.alertTimer <= 0) {
        g.state = 'routine';
        g.target = null;
        g.pauseTimer = 1;
        this.alertTimer = 0;
        this.toast('Briggs lost you. He pretends he meant to walk here all along. 😤', 'info');
      }
      return;
    }

    // patrol
    if (g.pauseTimer > 0) {
      g.pauseTimer -= dt;
      g.moving = false;
    } else if (!g.target || g.stepToward(g.target, dt, 2.0)) {
      const posts = this.world.guardPosts;
      g.target = posts[Math.floor(Math.random() * posts.length)].clone();
      g.pauseTimer = 2 + Math.random() * 3;
    }
    circleVsAABBs(g.pos, 0.35, this.world.colliders);

    // guard personally witnessing blatant crime
    if (this.canSee(g, this.playerPos) && this.carrying) {
      this.toast('Briggs saw you carrying a "mannequin". He is not buying it. 🚨', 'warn');
      g.state = 'hunt';
      g.lastSeenPlayer.copy(this.playerPos);
      this.alertTimer = Math.max(this.alertTimer, 15);
      this.synth.alarm();
      if (this.disguiseOf) this.blowDisguise();
      return;
    }

    // guard stumbling onto evidence → immediate hunt (he blames the shifty new guy)
    for (const b of this.npcs) {
      if (b === g || b.state !== 'out' || !b.group.visible) continue;
      if (this.canSee(g, b.pos)) {
        this.toast(`Briggs found ${b.name}'s body. He has decided it was you. 🚨`, 'warn');
        g.state = 'hunt';
        g.lastSeenPlayer.copy(this.playerPos);
        this.alertTimer = Math.max(this.alertTimer, 14);
        this.synth.alarm();
        return;
      }
    }
    for (const s of this.blood) {
      if (this.canSee(g, s.pos, 0.9)) {
        this.toast('Briggs found a bloodstain and is connecting dots that don\'t exist. 🚨', 'warn');
        g.state = 'hunt';
        g.lastSeenPlayer.copy(this.playerPos);
        this.alertTimer = Math.max(this.alertTimer, 12);
        this.synth.alarm();
        return;
      }
    }
  }

  private updateInteractions(dt: number) {
    this.prompt = '';

    // channel progress (hold E at terminal / hold E to mop)
    if (this.channelT >= 0) {
      const targetPos = this.channelMode === 'terminal' ? this.world.terminalPos : this.channelSplat?.pos ?? null;
      const near = targetPos !== null && this.playerPos.distanceTo(targetPos) < INTERACT_RANGE + 0.3;
      if (!this.keys.has('e') || !near) {
        this.channelT = -1;
        this.channelSplat = null;
        this.toast(this.channelMode === 'terminal' ? 'Download aborted. The progress bar judged you. ⏸️' : 'Mopping abandoned. The stain remains. Judgy stain. ⏸️', 'info');
      } else {
        const duration = this.channelMode === 'terminal' ? CHANNEL_TIME : MOP_TIME;
        this.channelT += dt;
        this.prompt = this.channelMode === 'terminal'
          ? `Stealing blueprints… ${Math.min(100, Math.round((this.channelT / duration) * 100))}%`
          : `Mopping up evidence… ${Math.min(100, Math.round((this.channelT / duration) * 100))}%`;
        if (this.channelT >= duration) {
          this.channelT = -1;
          if (this.channelMode === 'terminal') {
            this.hasBlueprint = true;
            this.synth.success();
            this.toast('BLUEPRINTS ACQUIRED. Now mail them out via the mail trolley. 📁', 'success');
          } else if (this.channelSplat) {
            this.removeSplat(this.channelSplat);
            this.channelSplat = null;
            this.stats.cleans++;
            this.synth.pickup();
            this.toast('Blood mopped. Just a janitor doing janitor things. Nothing to see here. 🧹✨', 'success');
          }
        }
      }
      return; // hands busy
    }

    // E pressed this frame? edge detection via key set + simple flag
    const wantInteract = this.keys.has('e');
    if (wantInteract && !this.eHeld) {
      this.eHeld = true;
      this.handleInteractPress();
    } else if (!wantInteract) {
      this.eHeld = false;
    }

    // compute contextual prompt
    this.prompt = this.computePrompt();
  }

  private eHeld = false;

  private handleInteractPress() {
    // 1. carrying a body near a hide spot -> hide it
    if (this.carrying) {
      const spot = this.nearestHideSpot();
      if (spot) { this.hideBodyIn(spot); return; }
      return;
    }
    // 2. deliver blueprint at trolley
    if (this.hasBlueprint && this.playerPos.distanceTo(this.world.trolleyPos) < INTERACT_RANGE + 0.6) {
      this.deliverBlueprint();
      return;
    }
    // 3. terminal
    if (!this.hasBlueprint && !this.blueprintSent && this.playerPos.distanceTo(this.world.terminalPos) < INTERACT_RANGE) {
      this.channelMode = 'terminal';
      this.channelT = 0;
      this.toast('Downloading blueprints… hold E. Try to look busy. 💻', 'info');
      return;
    }
    // 4. mop blood (requires mop)
    if (this.hasMop) {
      const splat = this.nearestSplat(INTERACT_RANGE);
      if (splat) {
        this.channelMode = 'mop';
        this.channelSplat = splat;
        this.channelT = 0;
        this.toast('Scrubbing… hold E. Hum something casual. 🧹', 'info');
        return;
      }
    }
    // 5. steal clothes from a body
    const body = this.nearestBody();
    if (body && !body.looted) {
      body.looted = true;
      this.disguiseOf = body.name;
      this.stats.disguises++;
      this.synth.pickup();
      this.toast(`You are now "definitely ${body.name}". Shirt's a bit tight. 👔`, 'success');
      return;
    }
    // 6. grab the mop from the supply closet
    const closet = this.world.hideSpots.find(s => s.id === 'closet')!;
    if (!this.hasMop && this.playerPos.distanceTo(closet.pos) < INTERACT_RANGE + 0.5) {
      this.equipMop();
      return;
    }
    // 7. photocopy your face (distraction)
    const printer = this.world.hideSpots.find(s => s.id === 'printer')!;
    if (this.photoCooldown <= 0 && this.playerPos.distanceTo(printer.pos) < INTERACT_RANGE + 0.5) {
      this.photocopyFace();
      return;
    }
  }

  private computePrompt(): string {
    if (this.carrying) {
      const spot = this.nearestHideSpot();
      if (spot) return `E — Hide ${this.carrying.name} in the ${spot.name} (${spot.occupants.length}/${spot.capacity})`;
      return `Q — Drop ${this.carrying.name} · Find somewhere… creative`;
    }
    if (this.hasBlueprint && this.playerPos.distanceTo(this.world.trolleyPos) < INTERACT_RANGE + 0.6) {
      return 'E — Mail the blueprints to your employers 📮';
    }
    if (!this.hasBlueprint && !this.blueprintSent && this.playerPos.distanceTo(this.world.terminalPos) < INTERACT_RANGE) {
      return 'Hold E — Steal the blueprints 💻';
    }
    if (this.hasMop && this.nearestSplat(INTERACT_RANGE)) {
      return 'Hold E — Mop up the evidence 🩸🧹';
    }
    const body = this.nearestBody();
    if (body) {
      return body.looted ? `Q — Pick up ${body.name}` : `Q — Pick up ${body.name} · E — "Borrow" their clothes 👔`;
    }
    const closet = this.world.hideSpots.find(s => s.id === 'closet')!;
    if (!this.hasMop && this.playerPos.distanceTo(closet.pos) < INTERACT_RANGE + 0.5) {
      return 'E — Grab the mop 🧹';
    }
    const printer = this.world.hideSpots.find(s => s.id === 'printer')!;
    if (this.playerPos.distanceTo(printer.pos) < INTERACT_RANGE + 0.5) {
      return this.photoCooldown > 0 ? `Printer is cooling down… (${Math.ceil(this.photoCooldown)}s)` : 'E — Photocopy your face. 50 copies. 📠😂';
    }
    return '';
  }

  private nearestBody(): NPC | null {
    let best: NPC | null = null;
    let bestDist = INTERACT_RANGE;
    for (const n of this.npcs) {
      if (n.state !== 'out' || !n.group.visible) continue;
      const d = Math.hypot(n.pos.x - this.playerPos.x, n.pos.z - this.playerPos.z);
      if (d < bestDist) { best = n; bestDist = d; }
    }
    return best;
  }

  private nearestHideSpot(): HideSpot | null {
    let best: HideSpot | null = null;
    let bestDist = INTERACT_RANGE + 0.4;
    for (const s of this.world.hideSpots) {
      if (s.occupants.length >= s.capacity) continue;
      const d = this.playerPos.distanceTo(s.pos);
      if (d < bestDist) { best = s; bestDist = d; }
    }
    return best;
  }

  private endGame(won: boolean, reason: string) {
    this.over = true;
    this.won = won;
    this.endReason = reason;
    document.exitPointerLock();
    if (won) this.synth.success();
    else this.synth.alarm();
    this.pushHud();
  }

  private pushHud() {
    this.onHud({
      started: this.started,
      paused: this.started && !this.over && !this.hasMouseControl(),
      over: this.over,
      won: this.won,
      endReason: this.endReason,
      prompt: this.prompt,
      channelProgress: this.channelT >= 0 ? Math.min(1, this.channelT / (this.channelMode === 'terminal' ? CHANNEL_TIME : MOP_TIME)) : -1,
      carrying: this.carrying?.name ?? null,
      disguise: this.disguiseOf,
      crouching: this.crouching,
      hasMop: this.hasMop,
      hasBlueprint: this.hasBlueprint,
      blueprintSent: this.blueprintSent,
      alert: this.alertTimer > 0 || this.guard?.state === 'hunt',
      timeLeft: Math.max(0, this.timeLeft),
      maxSuspicion: this.maxSuspicionValue,
      beingWatched: this.beingWatched,
      objectives: [
        { label: 'Infiltrate the server room', done: this.hasBlueprint || this.blueprintSent || this.channelT >= 0 },
        { label: 'Steal the blueprints', done: this.hasBlueprint || this.blueprintSent },
        { label: 'Mail them out via the mail trolley', done: this.blueprintSent },
        { label: 'Don\'t get caught (optional-ish)', done: false },
      ],
      stats: { ...this.stats },
    });
  }
}
