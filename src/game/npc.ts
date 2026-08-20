import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import * as SkeletonUtils from 'three/examples/jsm/utils/SkeletonUtils.js';
import type * as CANNON from 'cannon-es';
import { Ragdoll } from './ragdoll';

export type Archetype = 'snoop' | 'slob' | 'gossip' | 'grifter' | 'drone' | 'guard';
export type NpcState = 'routine' | 'curious' | 'panic' | 'report' | 'hunt' | 'seated' | 'out' | 'hidden';

export interface ArchetypeSpec {
  range: number;        // vision range (m)
  fov: number;          // half-angle in radians
  rate: number;         // suspicion gain multiplier
  speed: number;
  reports: boolean;     // runs to security after panicking
  label: string;
  color: number;
}

export const ARCHETYPES: Record<Archetype, ArchetypeSpec> = {
  snoop:   { range: 16, fov: 1.3, rate: 2.0, speed: 2.6, reports: true,  label: 'The Snoop',   color: 0xc03a5a },
  gossip:  { range: 12, fov: 1.15, rate: 1.5, speed: 2.4, reports: true, label: 'The Gossip',  color: 0xd070c0 },
  drone:   { range: 10, fov: 1.05, rate: 1.0, speed: 2.2, reports: true, label: 'Coworker',    color: 0x4a7dc0 },
  grifter: { range: 10, fov: 1.05, rate: 0.6, speed: 2.3, reports: false, label: 'The Grifter', color: 0x9a7a2a },
  slob:    { range: 6,  fov: 1.2, rate: 0.5, speed: 1.8, reports: true,  label: 'The Slob',    color: 0x6a8a5a },
  guard:   { range: 15, fov: 1.25, rate: 3.0, speed: 4.4, reports: false, label: 'Security',    color: 0x30343c },
};

function makeNameSprite(name: string, label: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 80;
  const ctx = canvas.getContext('2d')!;
  ctx.fillStyle = 'rgba(10,12,16,0.55)';
  ctx.roundRect(8, 4, 240, 72, 12);
  ctx.fill();
  ctx.fillStyle = '#fff';
  ctx.font = 'bold 34px Inter, sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText(name, 128, 40);
  ctx.fillStyle = '#ffd76a';
  ctx.font = '22px Inter, sans-serif';
  ctx.fillText(label, 128, 66);
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(canvas), depthTest: false, transparent: true }));
  sprite.scale.set(2.4, 0.75, 1);
  return sprite;
}

function makeEmojiSprite(emoji: string, size = 1): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 128;
  canvas.height = 128;
  const ctx = canvas.getContext('2d')!;
  ctx.font = '96px serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(emoji, 64, 72);
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(canvas), depthTest: false, transparent: true }));
  sprite.scale.set(size, size, 1);
  return sprite;
}

const cGreen = new THREE.Color(0x39d97a);
const cYellow = new THREE.Color(0xffd23a);
const cRed = new THREE.Color(0xff3b30);

export class NPC {
  // ---------- shared rigged asset ----------
  private static template: THREE.Object3D | null = null;
  private static clips: THREE.AnimationClip[] = [];
  private static scaleFactor = 1;
  static modelLoaded = false;

  static async loadAssets(): Promise<void> {
    try {
      const gltf = await new GLTFLoader().loadAsync('/models/soldier.glb');
      const scene = gltf.scene;
      const bbox = new THREE.Box3().setFromObject(scene);
      const height = bbox.max.y - bbox.min.y;
      NPC.scaleFactor = height > 0.1 ? 1.78 / height : 1;
      NPC.template = scene;
      NPC.clips = gltf.animations;
      NPC.modelLoaded = true;
    } catch (err) {
      console.warn('Soldier model failed to load, using primitive fallback', err);
      NPC.modelLoaded = false;
    }
  }

  private static findClip(re: RegExp, fallback: number): THREE.AnimationClip | null {
    if (NPC.clips.length === 0) return null;
    return NPC.clips.find(c => re.test(c.name)) ?? NPC.clips[Math.min(fallback, NPC.clips.length - 1)];
  }

  // ---------- instance ----------
  name: string;
  archetype: Archetype;
  spec: ArchetypeSpec;
  group = new THREE.Group();
  susBar!: THREE.Mesh;
  state: NpcState = 'routine';
  suspicion = 0;
  gossipSpreadDone = false;
  looted = false;          // clothes stolen
  home: THREE.Vector3;
  target: THREE.Vector3 | null = null;
  pauseTimer = 0;
  facing = 0;              // yaw
  zzz: THREE.Sprite | null = null;
  bang: THREE.Sprite | null = null;
  reportTarget: THREE.Vector3 | null = null;
  lastSeenPlayer = new THREE.Vector3();
  lostSightTimer = 0;
  distractTimer = 0;
  creepToastDone = false;
  crabToastDone = false;
  moving = false;

  // curiosity / panic
  investigateTarget: THREE.Vector3 | null = null;
  investigateRef: object | null = null;
  investigateKind: 'blood' | 'body' | null = null;
  panicTimer = 0;
  panicDuration = 0;
  panicKind: 'blood' | 'body' | null = null;
  panicRef: object | null = null;
  private panicShownSecond = -1;

  // rigged model / animation / ragdoll
  mixer: THREE.AnimationMixer | null = null;
  private idleAction: THREE.AnimationAction | null = null;
  private walkAction: THREE.AnimationAction | null = null;
  private currentAction: THREE.AnimationAction | null = null;
  ragdoll: Ragdoll | null = null;
  poolSpawned = false;

  private emoteSprite: THREE.Sprite | null = null;

  constructor(name: string, archetype: Archetype, pos: THREE.Vector3) {
    this.name = name;
    this.archetype = archetype;
    this.spec = ARCHETYPES[archetype];
    this.home = pos.clone();
    this.group.position.copy(pos);

    if (NPC.template) {
      const model = SkeletonUtils.clone(NPC.template);
      model.scale.setScalar(NPC.scaleFactor);
      const tint = new THREE.Color(this.spec.color);
      model.traverse(obj => {
        const mesh = obj as THREE.Mesh;
        if (mesh.isMesh) {
          mesh.castShadow = true;
          mesh.frustumCulled = false; // skinned mesh bounds go stale during ragdoll
          const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
          mesh.material = Array.isArray(mesh.material)
            ? mats.map(m => this.tintMaterial(m as THREE.MeshStandardMaterial, tint))
            : this.tintMaterial(mats[0] as THREE.MeshStandardMaterial, tint);
        }
      });
      this.group.add(model);

      this.mixer = new THREE.AnimationMixer(model);
      const idle = NPC.findClip(/idle/i, 0);
      const walk = NPC.findClip(/walk/i, 1);
      if (idle) this.idleAction = this.mixer.clipAction(idle);
      if (walk) this.walkAction = this.mixer.clipAction(walk);
      if (this.idleAction) {
        this.idleAction.play();
        this.currentAction = this.idleAction;
      }
    } else {
      // primitive fallback
      const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.32, 0.7, 6, 12), new THREE.MeshStandardMaterial({ color: this.spec.color, roughness: 0.85 }));
      body.position.y = 0.85;
      body.castShadow = true;
      this.group.add(body);
      const head = new THREE.Mesh(new THREE.SphereGeometry(0.24, 14, 12), new THREE.MeshStandardMaterial({ color: 0xe8b98a, roughness: 0.8 }));
      head.position.y = 1.62;
      this.group.add(head);
    }

    // name label
    const label = makeNameSprite(name, this.spec.label);
    label.position.y = 2.25;
    this.group.add(label);
    // suspicion bar
    this.susBar = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 0.12), new THREE.MeshBasicMaterial({ color: 0x39d97a, transparent: true, opacity: 0.95, depthTest: false }));
    this.susBar.position.y = 2.0;
    this.susBar.visible = false;
    this.susBar.renderOrder = 5;
    this.group.add(this.susBar);

    if (archetype === 'slob') {
      this.state = 'seated';
      this.zzz = makeEmojiSprite('💤', 0.8);
      this.zzz.position.y = 2.7;
      this.group.add(this.zzz);
    }
  }

  private tintMaterial(m: THREE.MeshStandardMaterial, tint: THREE.Color): THREE.Material {
    const c = m.clone();
    c.color = m.color.clone().lerp(tint, 0.55);
    return c;
  }

  get pos(): THREE.Vector3 {
    return this.group.position;
  }

  get awake(): boolean {
    return this.state !== 'out' && this.state !== 'hidden';
  }

  addSuspicion(amount: number) {
    if (!this.awake) return;
    this.suspicion = Math.min(100, this.suspicion + amount);
  }

  setMoving(moving: boolean, run = false) {
    if (!this.mixer || !this.idleAction || !this.walkAction || !this.currentAction) return;
    const next = moving ? this.walkAction : this.idleAction;
    this.walkAction.timeScale = run ? 1.7 : 1;
    if (next === this.currentAction) return;
    next.reset().fadeIn(0.18).play();
    this.currentAction.fadeOut(0.18);
    this.currentAction = next;
  }

  /** Move with collision resolved by caller. Returns true on arrival. */
  stepToward(target: THREE.Vector3, dt: number, speed = this.spec.speed): boolean {
    const dx = target.x - this.pos.x;
    const dz = target.z - this.pos.z;
    const dist = Math.hypot(dx, dz);
    if (dist < 0.35) {
      this.moving = false;
      return true;
    }
    const step = Math.min(dist, speed * dt);
    this.pos.x += (dx / dist) * step;
    this.pos.z += (dz / dist) * step;
    this.moving = true;
    const desired = Math.atan2(dx, dz);
    let diff = desired - this.facing;
    while (diff > Math.PI) diff -= Math.PI * 2;
    while (diff < -Math.PI) diff += Math.PI * 2;
    this.facing += diff * Math.min(1, dt * 8);
    this.group.rotation.y = this.facing;
    return false;
  }

  /** Knockout: ragdoll if we have a skeleton, else classic pratfall. */
  knockOut(phys: CANNON.World | null, flopFrom?: THREE.Vector3) {
    this.state = 'out';
    this.suspicion = 0;
    this.investigateTarget = null;
    this.investigateRef = null;
    this.panicTimer = 0;
    this.panicRef = null;
    this.poolSpawned = false;
    this.clearEmote();
    if (this.bang) { this.group.remove(this.bang); this.bang = null; }
    this.susBar.visible = false;
    this.moving = false;

    this.mixer?.stopAllAction();
    this.mixer = null;

    if (phys && NPC.template && this.mixer !== undefined) {
      this.ragdoll = new Ragdoll(phys, this.group, flopFrom);
      if (!this.ragdoll.active) this.ragdoll = null;
    }
    if (!this.ragdoll) {
      // fallback: dramatic floor collapse
      this.group.rotation.set(Math.PI / 2, this.group.rotation.y, 0);
      this.group.position.y = 0.35;
    }
    if (!this.zzz) {
      this.zzz = makeEmojiSprite('💤', 0.7);
      this.zzz.position.y = 1.4;
      this.group.add(this.zzz);
    }
  }

  /** Cancel physics when the body is picked up / hidden. */
  clearRagdoll() {
    if (this.ragdoll) {
      this.ragdoll.dispose();
      this.ragdoll = null;
    }
  }

  startCurious(target: THREE.Vector3, ref: object, kind: 'blood' | 'body') {
    this.state = 'curious';
    this.investigateTarget = target.clone();
    this.investigateRef = ref;
    this.investigateKind = kind;
    this.showEmote('❓');
  }

  startPanic(kind: 'blood' | 'body', ref: object, duration: number) {
    this.state = 'panic';
    this.panicKind = kind;
    this.panicRef = ref;
    this.panicTimer = duration;
    this.panicDuration = duration;
    this.panicShownSecond = -1;
    this.moving = false;
  }

  /** Ticks the panic countdown; returns true when it expires. */
  updatePanic(dt: number): boolean {
    this.panicTimer -= dt;
    const sec = Math.max(0, Math.ceil(this.panicTimer));
    if (sec !== this.panicShownSecond) {
      this.panicShownSecond = sec;
      this.showEmote(sec > 0 ? `😱${sec}` : '😱');
    }
    return this.panicTimer <= 0;
  }

  shrugItOff() {
    this.state = 'routine';
    this.investigateTarget = null;
    this.investigateRef = null;
    this.panicRef = null;
    this.panicTimer = 0;
    this.target = null;
    this.pauseTimer = 2;
    this.clearEmote();
  }

  startReport(guardPos: THREE.Vector3) {
    this.state = 'report';
    this.reportTarget = guardPos.clone();
    this.clearEmote();
    if (!this.bang) {
      this.bang = makeEmojiSprite('❗', 0.9);
      this.bang.position.y = 2.75;
      this.group.add(this.bang);
    }
  }

  calmDown() {
    this.shrugItOff();
    this.suspicion = 30;
    if (this.bang) { this.group.remove(this.bang); this.bang = null; }
  }

  showEmote(emoji: string) {
    this.clearEmote();
    this.emoteSprite = makeEmojiSprite(emoji, 0.9);
    this.emoteSprite.position.y = 2.75;
    this.group.add(this.emoteSprite);
  }

  clearEmote() {
    if (this.emoteSprite) {
      this.group.remove(this.emoteSprite);
      this.emoteSprite = null;
    }
  }

  updateVisuals(camera: THREE.Camera) {
    if (this.awake && this.state !== 'seated' && this.suspicion > 1) {
      this.susBar.visible = true;
      const t = this.suspicion / 100;
      const col = new THREE.Color();
      if (t < 0.5) col.lerpColors(cGreen, cYellow, t * 2);
      else col.lerpColors(cYellow, cRed, (t - 0.5) * 2);
      (this.susBar.material as THREE.MeshBasicMaterial).color.copy(col);
      this.susBar.scale.x = Math.max(0.05, t);
      this.susBar.quaternion.copy(camera.quaternion);
    } else {
      this.susBar.visible = false;
    }
    if (this.zzz) this.zzz.position.y = (this.state === 'out' ? 1.4 : 2.7) + Math.sin(performance.now() / 400) * 0.08;
  }
}
