import * as THREE from 'three';
import * as CANNON from 'cannon-es';

/** One ragdoll part: a physics body glued to a skeleton bone (position-driven). */
interface RagdollPart {
  bone: THREE.Bone;
  body: CANNON.Body;
}

interface PartDef {
  key: string;
  match: RegExp;
  parentKey: string | null;
  mass: number;
  radius: number;
}

// mixamorig-style skeleton (Soldier.glb): Hips -> Spine1 -> Head, arms, legs
const PART_DEFS: PartDef[] = [
  { key: 'hips', match: /hips$/i, parentKey: null, mass: 5, radius: 0.18 },
  { key: 'chest', match: /spine1$/i, parentKey: 'hips', mass: 3, radius: 0.16 },
  { key: 'head', match: /head$/i, parentKey: 'chest', mass: 1.2, radius: 0.13 },
  { key: 'lArm', match: /leftarm$/i, parentKey: 'chest', mass: 0.7, radius: 0.1 },
  { key: 'lFore', match: /leftforearm$/i, parentKey: 'lArm', mass: 0.5, radius: 0.09 },
  { key: 'rArm', match: /rightarm$/i, parentKey: 'chest', mass: 0.7, radius: 0.1 },
  { key: 'rFore', match: /rightforearm$/i, parentKey: 'rArm', mass: 0.5, radius: 0.09 },
  { key: 'lUpLeg', match: /leftupleg$/i, parentKey: 'hips', mass: 1.1, radius: 0.12 },
  { key: 'lLoLeg', match: /leftleg$/i, parentKey: 'lUpLeg', mass: 0.9, radius: 0.1 },
  { key: 'rUpLeg', match: /rightupleg$/i, parentKey: 'hips', mass: 1.1, radius: 0.12 },
  { key: 'rLoLeg', match: /rightleg$/i, parentKey: 'rUpLeg', mass: 0.9, radius: 0.1 },
];

const SETTLE_SECONDS = 2.8;

/**
 * Bone-driven ragdoll: snapshots bone world positions into a constrained
 * cannon-es body chain, lets gravity do slapstick, writes positions back
 * into the skeleton so the skinned mesh crumples.
 */
export class Ragdoll {
  private parts: RagdollPart[] = [];
  private constraints: CANNON.DistanceConstraint[] = [];
  private age = 0;
  private phys: CANNON.World;
  private root: THREE.Object3D;
  active = true;

  constructor(
    phys: CANNON.World,
    root: THREE.Object3D,
    flopDirection?: THREE.Vector3,
  ) {
    this.phys = phys;
    this.root = root;
    root.updateWorldMatrix(true, true);

    // collect bones by matcher
    const boneByKey = new Map<string, THREE.Bone>();
    root.traverse(obj => {
      if ((obj as THREE.Bone).isBone) {
        const name = obj.name.replace(/^mixamorig:?/i, '');
        for (const def of PART_DEFS) {
          if (!boneByKey.has(def.key) && def.match.test(name)) {
            boneByKey.set(def.key, obj as THREE.Bone);
          }
        }
      }
    });
    if (!boneByKey.has('hips')) {
      this.active = false;
      return; // no skeleton -> caller falls back to simple lying pose
    }

    const bodyByKey = new Map<string, CANNON.Body>();
    const worldPos = (b: THREE.Bone) => b.getWorldPosition(new THREE.Vector3());

    for (const def of PART_DEFS) {
      const bone = boneByKey.get(def.key);
      if (!bone) continue;
      const p = worldPos(bone);
      const body = new CANNON.Body({
        mass: def.mass,
        shape: new CANNON.Sphere(def.radius),
        position: new CANNON.Vec3(p.x, Math.max(p.y, def.radius + 0.02), p.z),
        linearDamping: 0.45,
        angularDamping: 0.7,
        allowSleep: false,
      });
      // comedic flop impulse
      const dir = flopDirection ?? new THREE.Vector3(Math.random() - 0.5, 0, Math.random() - 0.5).normalize();
      body.velocity.set(dir.x * 2.2, 1.4, dir.z * 2.2);
      this.phys.addBody(body);
      this.parts.push({ bone, body });
      bodyByKey.set(def.key, body);
    }

    for (const def of PART_DEFS) {
      if (!def.parentKey) continue;
      const child = bodyByKey.get(def.key);
      const parent = bodyByKey.get(def.parentKey);
      const childBone = boneByKey.get(def.key);
      const parentBone = boneByKey.get(def.parentKey);
      if (!child || !parent || !childBone || !parentBone) continue;
      const rest = worldPos(childBone).distanceTo(worldPos(parentBone));
      const c = new CANNON.DistanceConstraint(child, parent, Math.max(rest, 0.12), 1e5);
      this.phys.addConstraint(c);
      this.constraints.push(c);
    }
  }

  /** Advance physics; returns false once settled (caller may stop updating). */
  update(dt: number): boolean {
    if (!this.active) return false;
    this.age += dt;

    for (const part of this.parts) {
      // keep the mess inside the office
      const p = part.body.position;
      p.x = Math.max(-31.3, Math.min(31.3, p.x));
      p.z = Math.max(-21.3, Math.min(21.3, p.z));

      const parent = part.bone.parent;
      if (!parent) continue;
      const local = parent.worldToLocal(new THREE.Vector3(p.x, p.y, p.z));
      part.bone.position.copy(local);
    }
    this.root.updateWorldMatrix(true, true);

    if (this.age >= SETTLE_SECONDS) {
      this.dispose();
      return false;
    }
    return true;
  }

  /** Remove physics bodies/constraints; the final pose stays baked into the bones. */
  dispose() {
    for (const c of this.constraints) this.phys.removeConstraint(c);
    for (const part of this.parts) this.phys.removeBody(part.body);
    this.constraints = [];
    this.parts = [];
    this.active = false;
  }
}

export function createPhysicsWorld(): CANNON.World {
  const world = new CANNON.World({ gravity: new CANNON.Vec3(0, -10.5, 0) });
  world.broadphase = new CANNON.SAPBroadphase(world);
  const ground = new CANNON.Body({ mass: 0, shape: new CANNON.Plane() });
  ground.quaternion.setFromEuler(-Math.PI / 2, 0, 0);
  world.addBody(ground);
  return world;
}
