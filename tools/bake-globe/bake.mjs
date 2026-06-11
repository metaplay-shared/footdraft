// Offline globe bake.
//
// Produces WebClient/wwwroot/globe/world.json: the stylized globe geometry the
// browser renderer (globe.js) loads directly, so the client does ZERO topojson
// parsing / earcut / CDN fetching at runtime.
//
// This file is the canonical home of the globe generation math: latLonToVec3,
// nearest-seed bloc assignment, the emitTriangle subdivision "holes fix", and
// the edge-classification that drops internal borders and keeps inter-bloc
// frontiers. It uses plain-JS vectors and the `earcut` package directly (the
// same earcut algorithm that THREE.ShapeUtils.triangulateShape wraps).
//
// Usage:  cd tools/bake-globe && npm install && npm run bake
// The world-atlas dataset is fetched once and cached next to this script.

import earcut from 'earcut';
import { feature } from 'topojson-client';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const OUT_DIR = join(HERE, '..', '..', 'WebClient', 'wwwroot', 'globe');
const OUT = join(OUT_DIR, 'world.json');
// Small companion consumed server-side (C#): topology only, no geometry — so the
// host never parses the multi-MB geometry just to know which city belongs to which bloc.
const OUT_META = join(OUT_DIR, 'world-meta.json');
const DATA_CACHE = join(HERE, 'countries-110m.json');
const DATA_URL = 'https://unpkg.com/world-atlas@2.0.2/countries-110m.json';

// ----------------------------------------------------------------------------
// Constants & palette
// ----------------------------------------------------------------------------
const R = 100;
const DEG = Math.PI / 180;
const FILL_R = R * 1.002;
const COAST_R = R * 1.004;
const FRONT_R = R * 1.006;
const SUB_THRESH2 = 6 * 6; // subdivide triangles whose longest edge exceeds ~6°

const FRONTIER_COL = '#e6f7ff';

const BLOCS = [
  { name: 'NORAM',   colorHex: '#ff4d4d', lat:  45, lon: -100 },
  { name: 'SOUTHAM', colorHex: '#ff8a3c', lat: -15, lon:  -60 },
  { name: 'EURO',    colorHex: '#3a82ff', lat:  50, lon:   12 },
  { name: 'AFRIKA',  colorHex: '#24d39a', lat:   2, lon:   20 },
  { name: 'EURASIA', colorHex: '#b06bff', lat:  62, lon:   95 },
  { name: 'ASIA',    colorHex: '#ffd23c', lat:  22, lon:  105 },
  { name: 'OCEANIA', colorHex: '#2fd0e0', lat: -25, lon:  140 },
];

// Antarctica is its own region, but NOT a nearest-seed competitor: a seed at the
// south pole would also swallow Tierra del Fuego, the Falklands and southern NZ.
// Instead it's assigned by feature name (see bake()), so the whole continent —
// mainland plus its fringe islands, which the 110m data splits into separate
// polygons — lands in one consistent bloc instead of scattering across SOUTHAM /
// OCEANIA by per-polygon centroid. It has no cities, so it's never playable.
const ANTARCTICA = { name: 'ANTARCTICA', colorHex: '#cfe9f5' };
const ALL_BLOCS = [...BLOCS, ANTARCTICA];
const ANTARCTICA_ID = BLOCS.length; // bloc id of ANTARCTICA in ALL_BLOCS

const CITY_LIST = [
  ['NEW YORK', 40.71, -74.0], ['LOS ANGELES', 34.05, -118.24], ['CHICAGO', 41.88, -87.63], ['MEXICO CITY', 19.43, -99.13],
  ['SAO PAULO', -23.55, -46.63], ['BUENOS AIRES', -34.60, -58.38], ['BOGOTA', 4.71, -74.07], ['LIMA', -12.05, -77.04],
  ['LONDON', 51.51, -0.13], ['PARIS', 48.86, 2.35], ['BERLIN', 52.52, 13.40], ['MADRID', 40.42, -3.70],
  ['CAIRO', 30.04, 31.24], ['LAGOS', 6.52, 3.38], ['JOHANNESBURG', -26.20, 28.04], ['NAIROBI', -1.29, 36.82],
  ['MOSCOW', 55.75, 37.62], ['NOVOSIBIRSK', 55.03, 82.92], ['ASTANA', 51.13, 71.43], ['KYIV', 50.45, 30.52],
  ['BEIJING', 39.90, 116.41], ['TOKYO', 35.69, 139.69], ['DELHI', 28.61, 77.21], ['SHANGHAI', 31.23, 121.47],
  ['MUMBAI', 19.08, 72.88], ['SEOUL', 37.57, 126.98], ['BANGKOK', 13.75, 100.50],
  ['SYDNEY', -33.87, 151.21], ['MELBOURNE', -37.81, 144.96], ['AUCKLAND', -36.85, 174.76], ['JAKARTA', -6.21, 106.85],
];

// ----------------------------------------------------------------------------
// Vector helpers (plain JS)
// ----------------------------------------------------------------------------
function latLonToVec3(lat, lon, radius = R) {
  const phi = (90 - lat) * DEG;
  const theta = (lon + 180) * DEG;
  return [
    -radius * Math.sin(phi) * Math.cos(theta),
     radius * Math.cos(phi),
     radius * Math.sin(phi) * Math.sin(theta),
  ];
}

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const dot3 = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
function normalize3(v) {
  const len = Math.hypot(v[0], v[1], v[2]) || 1;
  return [v[0] / len, v[1] / len, v[2] / len];
}

const seedVecs = BLOCS.map(b => normalize3(latLonToVec3(b.lat, b.lon, 1)));
function blocOfDir(dir) {
  let best = 0, bestDot = -2;
  for (let k = 0; k < seedVecs.length; k++) {
    const d = dot3(dir, seedVecs[k]);
    if (d > bestDot) { bestDot = d; best = k; }
  }
  return best;
}
const blocAt = (lat, lon) => blocOfDir(normalize3(latLonToVec3(lat, lon, 1)));

function hexToRgb(hex) {
  const n = parseInt(hex.slice(1), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

// ----------------------------------------------------------------------------
// Polygon helpers
// ----------------------------------------------------------------------------
function ringStrip(ring) {
  if (ring.length > 1) {
    const a = ring[0], b = ring[ring.length - 1];
    if (a[0] === b[0] && a[1] === b[1]) return ring.slice(0, -1);
  }
  return ring;
}

// Unwrap longitudes for a polygon that straddles the antimeridian.
function maybeUnwrap(rings) {
  let min = Infinity, max = -Infinity;
  for (const [lon] of rings[0]) { if (lon < min) min = lon; if (lon > max) max = lon; }
  if (max - min <= 180) return rings;
  return rings.map(r => r.map(([lon, lat]) => [lon < 0 ? lon + 360 : lon, lat]));
}

// Recursively split a (lon,lat) triangle so its pieces hug the sphere instead of
// sagging below it (flat chords of large triangles dip under the ocean sphere
// and read as holes). Linear midpoints stay inside the polygon, so no re-test.
function emitTriangle(a, b, c, out, depth) {
  const dab = (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2;
  const dbc = (b[0] - c[0]) ** 2 + (b[1] - c[1]) ** 2;
  const dca = (c[0] - a[0]) ** 2 + (c[1] - a[1]) ** 2;
  if (depth < 6 && Math.max(dab, dbc, dca) > SUB_THRESH2) {
    const ab = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
    const bc = [(b[0] + c[0]) / 2, (b[1] + c[1]) / 2];
    const ca = [(c[0] + a[0]) / 2, (c[1] + a[1]) / 2];
    emitTriangle(a, ab, ca, out, depth + 1);
    emitTriangle(ab, b, bc, out, depth + 1);
    emitTriangle(ca, bc, c, out, depth + 1);
    emitTriangle(ab, bc, ca, out, depth + 1);
    return;
  }
  const va = latLonToVec3(a[1], a[0], FILL_R);
  const vb = latLonToVec3(b[1], b[0], FILL_R);
  const vc = latLonToVec3(c[1], c[0], FILL_R);
  out.push(va[0], va[1], va[2], vb[0], vb[1], vb[2], vc[0], vc[1], vc[2]);
}

// Total signed longitude swept while walking a ring. ≈±360° means the ring
// encloses a pole (e.g. Antarctica's coastline): its interior runs off the
// bottom of the lon/lat map to the pole, which is NOT one of its vertices, so
// earcut fills it inside-out (a blob with a continent-shaped hole). ≈0 for an
// ordinary ring.
function lonWinding(ring) {
  let w = 0;
  for (let i = 0; i < ring.length; i++) {
    const a = ring[i], b = ring[(i + 1) % ring.length];
    let d = b[0] - a[0];
    if (d > 180) d -= 360; else if (d < -180) d += 360;
    w += d;
  }
  return w;
}

// Azimuthal projection about a pole and its inverse. `rho` is the angular
// distance from the enclosed pole in degrees (0 at the pole), `theta` the
// longitude; (x, y) is the resulting plane. cos/sin of the raw longitude are
// continuous across the ±180° seam, so this needs NO antimeridian unwrap — the
// seam simply isn't special in this projection.
function poleProject(lon, lat, poleSign) {
  const rho = poleSign < 0 ? lat + 90 : 90 - lat;
  const th = lon * DEG;
  return [rho * Math.cos(th), rho * Math.sin(th)];
}
function poleUnprojectVec3(p, poleSign, radius) {
  const rho = Math.hypot(p[0], p[1]);
  const lat = poleSign < 0 ? rho - 90 : 90 - rho;
  const lon = Math.atan2(p[1], p[0]) / DEG;
  return latLonToVec3(lat, lon, radius);
}

// emitTriangle's twin for pole-cap faces: subdivide and sphere-project in the
// azimuthal plane (where the pole is just the origin and linear midpoints stay
// well-behaved right up to it) instead of in lon/lat (where they don't).
function emitTrianglePolar(a, b, c, poleSign, out, depth) {
  const dab = (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2;
  const dbc = (b[0] - c[0]) ** 2 + (b[1] - c[1]) ** 2;
  const dca = (c[0] - a[0]) ** 2 + (c[1] - a[1]) ** 2;
  if (depth < 6 && Math.max(dab, dbc, dca) > SUB_THRESH2) {
    const ab = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
    const bc = [(b[0] + c[0]) / 2, (b[1] + c[1]) / 2];
    const ca = [(c[0] + a[0]) / 2, (c[1] + a[1]) / 2];
    emitTrianglePolar(a, ab, ca, poleSign, out, depth + 1);
    emitTrianglePolar(ab, b, bc, poleSign, out, depth + 1);
    emitTrianglePolar(ca, bc, c, poleSign, out, depth + 1);
    emitTrianglePolar(ab, bc, ca, poleSign, out, depth + 1);
    return;
  }
  const va = poleUnprojectVec3(a, poleSign, FILL_R);
  const vb = poleUnprojectVec3(b, poleSign, FILL_R);
  const vc = poleUnprojectVec3(c, poleSign, FILL_R);
  out.push(va[0], va[1], va[2], vb[0], vb[1], vb[2], vc[0], vc[1], vc[2]);
}

// Fill a pole-enclosing ring. The old approach fanned triangles from the pole to
// each coast edge, which only tiles cleanly when the coast is single-valued in
// longitude; Antarctica's coast bends back on itself (the peninsula, the Ross and
// Weddell embayments), so the fan overlapped and flipped, drawing bright radial
// seams to the pole and mangling every concave bay. Instead we azimuthally
// project the ring about the pole — turning the pole-enclosing ring into an
// ordinary planar polygon with the pole as an interior (non-vertex) point — and
// let earcut triangulate it properly, concavities and all.
function triangulatePoleCap(ring, out) {
  const pts = ringStrip(ring);
  if (pts.length < 3) return;
  let latSum = 0;
  for (const [, lat] of pts) latSum += lat;
  const poleSign = latSum < 0 ? -1 : 1;

  const coords = [];
  for (const [lon, lat] of pts) {
    const q = poleProject(lon, lat, poleSign);
    coords.push(q[0], q[1]);
  }

  let faces;
  try { faces = earcut(coords, null, 2); }
  catch { return; }

  for (let i = 0; i < faces.length; i += 3) {
    const ia = faces[i] * 2, ib = faces[i + 1] * 2, ic = faces[i + 2] * 2;
    emitTrianglePolar(
      [coords[ia], coords[ia + 1]],
      [coords[ib], coords[ib + 1]],
      [coords[ic], coords[ic + 1]],
      poleSign, out, 0);
  }
}

// Triangulate one polygon (outer ring + holes) with earcut, then sphere-project
// each face through emitTriangle. Appends sphere positions to `out`.
function triangulatePolygon(rings, out) {
  const stripped = rings.map(ringStrip);
  if (stripped[0].length < 3) return;

  // A ring that winds a full turn in longitude encloses a pole; earcut can't
  // triangulate that in lon/lat, so fill it as a pole cap instead. (Holes are
  // ignored — none exist for the only such land mass, Antarctica, at 110m.)
  if (Math.abs(lonWinding(stripped[0])) > 180) { triangulatePoleCap(stripped[0], out); return; }

  const unwrapped = maybeUnwrap(stripped);
  const contour = unwrapped[0];
  const holes = unwrapped.slice(1);

  // Flatten to earcut input: [x0,y0,x1,y1,...] with hole start indices.
  const coords = [];
  const holeIndices = [];
  for (const [lon, lat] of contour) coords.push(lon, lat);
  for (const hole of holes) {
    holeIndices.push(coords.length / 2);
    for (const [lon, lat] of hole) coords.push(lon, lat);
  }

  let faces;
  try { faces = earcut(coords, holeIndices.length ? holeIndices : null, 2); }
  catch { return; }

  for (let i = 0; i < faces.length; i += 3) {
    const ia = faces[i] * 2, ib = faces[i + 1] * 2, ic = faces[i + 2] * 2;
    emitTriangle(
      [coords[ia], coords[ia + 1]],
      [coords[ib], coords[ib + 1]],
      [coords[ic], coords[ic + 1]],
      out, 0);
  }
}

function registerEdges(rings, bloc, edges) {
  // Unwrap the same way the fills do, so antimeridian-crossing polygons (e.g. Russia, Fiji)
  // don't emit a coastline/frontier chord straight across the globe at the ±180° seam.
  const unwrapped = maybeUnwrap(rings.map(ringStrip));
  for (const r of unwrapped) {
    for (let i = 0; i < r.length; i++) {
      const a = r[i], b = r[(i + 1) % r.length];
      const ka = `${a[0].toFixed(4)},${a[1].toFixed(4)}`;
      const kb = `${b[0].toFixed(4)},${b[1].toFixed(4)}`;
      const key = ka < kb ? ka + '|' + kb : kb + '|' + ka;
      let e = edges.get(key);
      if (!e) { e = { a, b, n: 1, bloc, multi: false }; edges.set(key, e); }
      else { e.n++; if (bloc !== e.bloc) e.multi = true; }
    }
  }
}

// ----------------------------------------------------------------------------
// Data load (fetch once, cache locally)
// ----------------------------------------------------------------------------
async function loadTopo() {
  if (existsSync(DATA_CACHE)) {
    return JSON.parse(await readFile(DATA_CACHE, 'utf8'));
  }
  console.log(`Fetching ${DATA_URL} (one-time)…`);
  const res = await fetch(DATA_URL);
  if (!res.ok) throw new Error(`Failed to fetch world-atlas: ${res.status}`);
  const text = await res.text();
  await writeFile(DATA_CACHE, text);
  console.log(`Cached to ${DATA_CACHE}`);
  return JSON.parse(text);
}

// ----------------------------------------------------------------------------
// Bake
// ----------------------------------------------------------------------------
async function bake() {
  const topo = await loadTopo();
  const land = feature(topo, topo.objects.countries);

  const fillsByBloc = ALL_BLOCS.map(() => []); // sphere positions per bloc
  const edges = new Map();

  for (const f of land.features) {
    const g = f.geometry;
    if (!g) continue;
    const polygons = g.type === 'Polygon' ? [g.coordinates]
                   : g.type === 'MultiPolygon' ? g.coordinates : [];
    if (!polygons.length) continue;

    // Antarctica is grouped as a whole by name (mainland + fringe islands) into its
    // own bloc, rather than letting each polygon pick a nearest seed independently.
    const isAntarctica = f.properties?.name === 'Antarctica';

    for (const poly of polygons) {
      // bloc = nearest seed to THIS polygon's 3D centroid (antimeridian-safe), not
      // the whole country's. A country's far-flung territories then land in their
      // own region — e.g. French Guiana joins SOUTHAM instead of inheriting EURO
      // from mainland France.
      const c = [0, 0, 0];
      for (const [lon, lat] of poly[0]) {
        const v = latLonToVec3(lat, lon, 1);
        c[0] += v[0]; c[1] += v[1]; c[2] += v[2];
      }
      const bloc = isAntarctica ? ANTARCTICA_ID : blocOfDir(normalize3(c));

      triangulatePolygon(poly, fillsByBloc[bloc]);
      registerEdges(poly, bloc, edges);
    }
  }

  // Merge per-bloc fills into one position buffer split into contiguous draw groups, one per bloc.
  // The renderer colours each group with a single material — so we ship 7 colours (in `blocs`)
  // instead of an RGB triple per vertex, roughly halving the fills payload and the GPU buffer.
  const fillPos = [];
  const fillGroups = []; // { blocId, start, count } in vertices
  fillsByBloc.forEach((verts, k) => {
    if (!verts.length) return;
    const start = fillPos.length / 3;
    for (let i = 0; i < verts.length; i++) fillPos.push(verts[i]);
    fillGroups.push({ blocId: k, start, count: verts.length / 3 });
  });

  // Classify edges: coastline (single owner, tinted) vs inter-bloc frontier;
  // internal same-bloc borders (n>=2 && !multi) are dropped.
  const coastPos = [], coastCol = [], frontPos = [];
  for (const e of edges.values()) {
    const pa = latLonToVec3(e.a[1], e.a[0], COAST_R);
    const pb = latLonToVec3(e.b[1], e.b[0], COAST_R);
    if (e.n === 1) {
      const [r, g, b] = hexToRgb(ALL_BLOCS[e.bloc].colorHex).map(v => v * 0.85);
      coastPos.push(pa[0], pa[1], pa[2], pb[0], pb[1], pb[2]);
      coastCol.push(r, g, b, r, g, b);
    } else if (e.multi) {
      const fa = latLonToVec3(e.a[1], e.a[0], FRONT_R);
      const fb = latLonToVec3(e.b[1], e.b[0], FRONT_R);
      frontPos.push(fa[0], fa[1], fa[2], fb[0], fb[1], fb[2]);
    }
  }

  // Blocs + cities (cities carry their baked sphere position, so the renderer
  // and the C# render contract can both reference them purely by id).
  const blocs = ALL_BLOCS.map((b, id) => ({ id, name: b.name, colorHex: b.colorHex }));
  const cities = CITY_LIST.map(([name, lat, lon], id) => {
    const blocId = blocAt(lat, lon);
    return { id, blocId, name, lat, lon, colorHex: ALL_BLOCS[blocId].colorHex };
  });

  const round = (arr) => arr.map(v => Math.round(v * 1000) / 1000);

  const world = {
    radius: R,
    frontierColorHex: FRONTIER_COL,
    blocs,
    cities,
    fills: { positions: round(fillPos), groups: fillGroups },
    frontiers: { positions: round(frontPos) },
    coastlines: { positions: round(coastPos), colors: round(coastCol) },
  };

  await mkdir(OUT_DIR, { recursive: true });
  await writeFile(OUT, JSON.stringify(world));

  // Topology-only companion for the C# host (blocs + cities, no geometry).
  await writeFile(OUT_META, JSON.stringify({ radius: R, blocs, cities }));

  console.log('Baked globe →', OUT);
  console.log('Baked meta  →', OUT_META);
  console.log(`  blocs:      ${blocs.length}`);
  console.log(`  cities:     ${cities.length}`);
  console.log(`  fill tris:  ${fillPos.length / 9}`);
  console.log(`  coastline segs: ${coastPos.length / 6}`);
  console.log(`  frontier segs:  ${frontPos.length / 6}`);
  const bytes = (await readFile(OUT)).length;
  console.log(`  size:       ${(bytes / 1024).toFixed(0)} KB`);
}

bake().catch(err => { console.error(err); process.exit(1); });
