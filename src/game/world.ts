import * as THREE from 'three';
import type { AABB, HideSpot, RoomId, WorldRefs } from './types';

const WALL_H = 3;

function mat(color: number, rough = 0.9): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({ color, roughness: rough });
}

export function buildWorld(scene: THREE.Scene): WorldRefs {
  const group = new THREE.Group();
  const colliders: AABB[] = [];
  const visionBlockers: AABB[] = [];

  const addBox = (
    x: number, y: number, z: number,
    w: number, h: number, d: number,
    color: number,
    opts: { solid?: boolean; blocksVision?: boolean; rotY?: number } = {},
  ): THREE.Mesh => {
    const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat(color));
    m.position.set(x, y, z);
    if (opts.rotY) m.rotation.y = opts.rotY;
    m.castShadow = h > 0.4;
    m.receiveShadow = true;
    group.add(m);
    if (opts.solid !== false && !opts.rotY) {
      const bb: AABB = { minX: x - w / 2, maxX: x + w / 2, minZ: z - d / 2, maxZ: z + d / 2 };
      colliders.push(bb);
      if (opts.blocksVision) visionBlockers.push(bb);
    }
    return m;
  };

  /** Wall between two points (axis aligned). */
  const addWall = (x1: number, z1: number, x2: number, z2: number, color = 0xd8d4cc) => {
    const w = Math.max(Math.abs(x2 - x1), 0.3);
    const d = Math.max(Math.abs(z2 - z1), 0.3);
    addBox((x1 + x2) / 2, WALL_H / 2, (z1 + z2) / 2, w, WALL_H, d, color, { blocksVision: true });
  };

  // ---------- Floor & ceiling ----------
  const floor = new THREE.Mesh(new THREE.BoxGeometry(64, 0.2, 44), mat(0x9aa3ad, 1));
  floor.position.y = -0.1;
  floor.receiveShadow = true;
  group.add(floor);

  // Carpet zones for visual interest
  const carpet = (x: number, z: number, w: number, d: number, color: number) => {
    const c = new THREE.Mesh(new THREE.PlaneGeometry(w, d), new THREE.MeshStandardMaterial({ color, roughness: 1 }));
    c.rotation.x = -Math.PI / 2;
    c.position.set(x, 0.01, z);
    c.receiveShadow = true;
    group.add(c);
  };
  carpet(0, 1, 40, 24, 0x8792a0);        // cubicle farm carpet
  carpet(22, -15, 20, 14, 0x6f8f7a);     // break room green
  carpet(0, 18, 64, 8, 0x8a6f4f);        // reception wood-ish
  carpet(-22, -17, 20, 10, 0x5a6472);    // server room dark

  // ---------- Outer walls ----------
  addWall(-32, -22, 32, -22);
  addWall(-32, 22, 32, 22);
  addWall(-32, -22, -32, 22);
  addWall(32, -22, 32, 22);

  // ---------- Server room (NW): x -32..-12, z -22..-12 ----------
  addWall(-32, -12, -12, -12);            // south wall
  addWall(-12, -22, -12, -18);            // east wall (top)
  addWall(-12, -15, -12, -12);            // east wall (bottom) -> door gap z -18..-15
  // server racks along north wall
  for (let i = 0; i < 5; i++) {
    const rx = -29 + i * 3.4;
    addBox(rx, 1.1, -20.8, 2.2, 2.2, 1.2, 0x22262e, { blocksVision: true });
    addBox(rx, 1.6, -20.15, 1.8, 0.25, 0.06, 0x35f0a0, { solid: false }); // blinkenlights
  }
  // blueprint terminal desk
  addBox(-22, 0.45, -18.2, 2.4, 0.9, 1.2, 0x6b5f4a);
  const terminal = addBox(-22, 1.25, -18.4, 1.1, 0.7, 0.12, 0x0af0ff, { solid: false });
  terminal.name = 'terminal';
  // restricted sign stripe
  carpet(-13.2, -16.5, 1.6, 3, 0xc03a2b);

  // ---------- Printer room (W): x -32..-22, z -12..-2 ----------
  addWall(-22, -12, -22, -8);             // east wall top
  addWall(-22, -6, -22, -2);              // east wall bottom -> door gap z -8..-6
  addWall(-32, -2, -28.5, -2);            // south wall left
  addWall(-24.5, -2, -22, -2);            // south wall right -> door gap x -28.5..-24.5
  // the legendary printer
  addBox(-27, 0.65, -10.5, 2.4, 1.3, 1.8, 0xe8e8e8);
  addBox(-27, 1.45, -10.5, 1.8, 0.3, 1.3, 0xbfc4cc, { solid: false });
  addBox(-30.5, 0.7, -5, 1.6, 1.4, 1.2, 0x8a94a6); // paper shelves

  // ---------- Break room (E): x 12..32, z -22..-8 (open concept) ----------
  addWall(12, -22, 12, -16);
  addWall(12, -12, 12, -8);
  // kitchen counter
  addBox(28, 0.5, -20.5, 6, 1, 1.4, 0x7d8aa0);
  addBox(25, 0.9, -20.4, 0.9, 0.8, 0.7, 0xc0c8d4, { solid: false }); // coffee machine
  // vending machine
  addBox(31, 1.1, -17, 1.6, 2.2, 1.2, 0xc23a55, { blocksVision: true });
  // water cooler
  addBox(14, 0.8, -20.8, 0.8, 1.6, 0.8, 0xbfd9e8);
  // beanbags (soft decor)
  for (const [bx, bz] of [[18, -18], [21, -14], [16, -11]] as const) {
    const b = new THREE.Mesh(new THREE.SphereGeometry(0.8, 12, 10), mat(0xe0a33a));
    b.scale.y = 0.55;
    b.position.set(bx, 0.4, bz);
    b.castShadow = true;
    group.add(b);
  }
  // THE MAIL TROLLEY
  addBox(26, 0.55, -12, 1.4, 1.1, 2.0, 0x9a6a3a);
  addBox(26, 1.2, -12, 1.5, 0.15, 2.1, 0x6e4a26, { solid: false });

  // ---------- Supply closet: x 22..32, z 8..13.5 ----------
  addWall(22, 8, 32, 8);
  addWall(22, 8, 22, 11);
  addWall(22, 12.5, 22, 13.5);            // door gap z 11..12.5
  addBox(30.5, 0.9, 12.5, 2.6, 1.8, 1.6, 0x7a6f5a); // shelving

  // ---------- Reception (S): z 14..22 ----------
  addBox(0, 0.6, 17.5, 8, 1.2, 1.6, 0x8a6f4f);      // front desk
  // elevator doors (decor)
  addBox(0, 1.6, 21.7, 4, 3.2, 0.2, 0xb8c0cc, { solid: false });
  addBox(0, 1.6, 21.55, 0.15, 3.2, 0.3, 0x666e7a, { solid: false });

  // ---------- Cubicle farm: 12 pods ----------
  const podCenters: [number, number][] = [];
  for (const px of [-15, -5, 5, 15]) {
    for (const pz of [-6, 1, 8]) podCenters.push([px, pz]);
  }
  const deskSpots: THREE.Vector3[] = [];
  for (const [cx, cz] of podCenters) {
    // partitions (vision blockers) — U shapes back to back
    addBox(cx, 0.7, cz - 2, 5.4, 1.4, 0.12, 0x7f8ba3, { blocksVision: true });
    addBox(cx, 0.7, cz + 2, 5.4, 1.4, 0.12, 0x7f8ba3, { blocksVision: true });
    addBox(cx - 2.7, 0.7, cz, 0.12, 1.4, 4, 0x7f8ba3, { blocksVision: true });
    addBox(cx + 2.7, 0.7, cz, 0.12, 1.4, 4, 0x7f8ba3, { blocksVision: true });
    // 4 desks
    for (const [dx, dz] of [[-1.2, -1], [1.2, -1], [-1.2, 1], [1.2, 1]] as const) {
      addBox(cx + dx, 0.4, cz + dz, 1.8, 0.08, 1.1, 0xd9d2c5, { solid: false });
      addBox(cx + dx, 0.2, cz + dz, 1.6, 0.4, 0.9, 0xb9b2a5);
      addBox(cx + dx, 0.75, cz + dz - 0.2, 0.7, 0.45, 0.08, 0x222831, { solid: false }); // monitor
      deskSpots.push(new THREE.Vector3(cx + dx, 0, cz + dz));
    }
  }

  // plants for morale
  for (const [px, pz] of [[-24, 5], [24, 3], [-8, 12], [8, -9.5], [0, 13], [-30, 0]] as const) {
    const pot = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.45, 0.6, 10), mat(0xa04a2e));
    pot.position.set(px, 0.3, pz);
    group.add(pot);
    const leaf = new THREE.Mesh(new THREE.SphereGeometry(0.6, 8, 8), mat(0x3f8a3a));
    leaf.position.set(px, 1.1, pz);
    leaf.castShadow = true;
    group.add(leaf);
    colliders.push({ minX: px - 0.4, maxX: px + 0.4, minZ: pz - 0.4, maxZ: pz + 0.4 });
  }

  // floor lamp (the future disguise of some poor soul)
  const lampBase = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.3, 0.1, 10), mat(0x555a63));
  lampBase.position.set(15, 0.05, -15);
  group.add(lampBase);

  scene.add(group);

  // ---------- Waypoints ----------
  const floorPts: THREE.Vector3[] = [];
  for (const wx of [-20, -10, 0, 10, 20]) {
    for (const wz of [-9.5, -2.5, 4.5, 11]) floorPts.push(new THREE.Vector3(wx, 0, wz));
  }
  const breakPts = [
    new THREE.Vector3(16, 0, -16), new THREE.Vector3(24, 0, -18),
    new THREE.Vector3(20, 0, -10), new THREE.Vector3(28, 0, -15),
    new THREE.Vector3(14, 0, -19),
  ];
  const printerPts = [new THREE.Vector3(-26.5, 0, -4), new THREE.Vector3(-29, 0, -8)];
  const corridorPts = [new THREE.Vector3(-12, 0, -16.5), new THREE.Vector3(-17, 0, -16.5)];
  const closetPts = [new THREE.Vector3(26, 0, 11), new THREE.Vector3(24, 0, 12)];

  const waypoints: Record<string, THREE.Vector3[]> = {
    floor: floorPts,
    break: breakPts,
    printer: printerPts,
    closet: closetPts,
    snoop: [...floorPts, ...breakPts, ...printerPts, ...corridorPts, ...closetPts],
    gossip: [...floorPts, ...breakPts, ...printerPts],
    drone: [...floorPts, ...breakPts],
    grifter: [...floorPts, ...closetPts, ...breakPts],
  };

  const hideSpots: HideSpot[] = [
    { id: 'printer', name: 'Office Printer', action: 'shoved under the office printer 📠', pos: new THREE.Vector3(-27, 0, -9), capacity: 1, occupants: [] },
    { id: 'trolley', name: 'Mail Trolley', action: 'wheeled toward the loading bay in the mail trolley 📮', pos: new THREE.Vector3(26, 0, -12), capacity: 1, occupants: [] },
    { id: 'rack', name: 'Server Rack', action: 'filed between the server racks 🗄️', pos: new THREE.Vector3(-25.5, 0, -20), capacity: 1, occupants: [] },
    { id: 'closet', name: 'Supply Closet', action: 'stacked neatly in the supply closet 🧹', pos: new THREE.Vector3(27, 0, 11), capacity: 2, occupants: [] },
    { id: 'lamp', name: 'Floor Lamp', action: 'promoted to floor lamp 💡', pos: new THREE.Vector3(15, 0, -15), capacity: 1, occupants: [] },
  ];

  const roomAt = (x: number, z: number): RoomId => {
    if (x >= -32 && x <= -12 && z >= -22 && z <= -12) return 'server';
    if (x >= -32 && x <= -22 && z >= -12 && z <= -2) return 'printer';
    if (x >= 12 && x <= 32 && z >= -22 && z <= -8) return 'break';
    if (x >= 22 && x <= 32 && z >= 8 && z <= 13.5) return 'closet';
    if (z >= 14) return 'reception';
    return 'floor';
  };

  return {
    group,
    colliders,
    visionBlockers,
    waypoints,
    hideSpots,
    terminalPos: new THREE.Vector3(-22, 0, -17.2),
    trolleyPos: new THREE.Vector3(26, 0, -12),
    slobDeskPos: new THREE.Vector3(-16.2, 0, 0),
    guardPosts: [
      new THREE.Vector3(0, 0, 17),
      new THREE.Vector3(-12, 0, 5),
      new THREE.Vector3(12, 0, 5),
      new THREE.Vector3(0, 0, -3),
    ],
    roomAt,
  };
}
