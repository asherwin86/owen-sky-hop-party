// Sky Hop Party online server.
// Free-for-all races: everyone runs the same course, first to the flag wins the round.
// Clients simulate their own player and stream its state; the server relays snapshots,
// hands out colours and start slots, decides the finish order and keeps a win tally per room.
const http = require('http');
const { WebSocketServer } = require('ws');

const PORT = process.env.PORT || 8080;
const TICK_HZ = 15;
const MAX_PLAYERS = 8;
const COUNTDOWN_MS = 3500;
const FINISH_GRACE_MS = 20000;     // after the winner finishes, everyone else gets this long
const RACE_CAP_MS = 5 * 60 * 1000;
const PUBLIC_CODE = 'PUBLIC';
const PUBLIC_COUNTDOWN_MS = 15000;
const PUBLIC_BREAK_MS = 12000;
const CODE_LETTERS = 'ABCDEFGHJKLMNPQRSTUVWXYZ';

const rooms = new Map();
let nextId = 1;

const server = http.createServer((req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/plain' });
  res.end('Sky Hop Party server is running. Rooms: ' + rooms.size + '\n');
});
const wss = new WebSocketServer({ server });

function makeCode() {
  for (let a = 0; a < 100; a++) {
    let c = '';
    for (let i = 0; i < 4; i++) c += CODE_LETTERS[Math.floor(Math.random() * CODE_LETTERS.length)];
    if (!rooms.has(c)) return c;
  }
  return null;
}
function cleanName(n) {
  const s = String(n || '').replace(/[^\w \-]/g, '').trim().slice(0, 12);
  return s || 'Hopper' + Math.floor(100 + Math.random() * 900);
}
function send(ws, o) { if (ws.readyState === 1) ws.send(JSON.stringify(o)); }
function broadcast(room, o, except) {
  const d = JSON.stringify(o);
  for (const p of room.players.values()) if (p !== except && p.ws.readyState === 1) p.ws.send(d);
}
function roomInfo(room) {
  return {
    t: 'room', code: room.code, hostId: room.hostId, isPublic: room.isPublic, phase: room.phase,
    players: [...room.players.values()].map(p => ({ id: p.id, name: p.name, color: p.color, wins: p.wins })),
  };
}

function createRoom(host, isPublic) {
  const code = isPublic ? PUBLIC_CODE : makeCode();
  if (!code) return null;
  const room = {
    code, isPublic: !!isPublic, hostId: isPublic ? 0 : host.id, phase: 'lobby',
    players: new Map(), nextStart: 0, raceStart: 0, firstFinish: 0, finishOrder: [], tick: null,
  };
  rooms.set(code, room);
  room.tick = setInterval(() => tickRoom(room), 1000 / TICK_HZ);
  return room;
}

function addPlayer(room, p) {
  const used = new Set([...room.players.values()].map(o => o.color));
  p.color = 0; while (used.has(p.color)) p.color++;
  p.room = room; p.st = null; p.finished = false; p.time = 0; p.wins = 0;
  room.players.set(p.id, p);
}

function removePlayer(p) {
  const room = p.room;
  if (!room) return;
  room.players.delete(p.id);
  p.room = null;
  if (room.players.size === 0) { clearInterval(room.tick); rooms.delete(room.code); return; }
  if (!room.isPublic && room.hostId === p.id) room.hostId = room.players.keys().next().value;
  broadcast(room, roomInfo(room));
  if (room.phase === 'racing') checkAllFinished(room);
}

const COURSES = 4;
function startRace(room, courseWanted) {
  let course = 0;
  if (room.isPublic) { room.courseNo = (room.courseNo === undefined ? Math.floor(Math.random() * COURSES) : room.courseNo + 1) % COURSES; course = room.courseNo; }
  else if (Number.isInteger(courseWanted)) course = Math.max(0, Math.min(COURSES - 1, courseWanted));
  room.phase = 'racing';
  room.nextStart = 0;
  room.raceStart = Date.now() + COUNTDOWN_MS;
  room.firstFinish = 0;
  room.finishOrder = [];
  const ids = [...room.players.keys()];
  for (let i = ids.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [ids[i], ids[j]] = [ids[j], ids[i]]; }
  ids.forEach((id, i) => { const p = room.players.get(id); p.slot = i; p.finished = false; p.st = null; });
  broadcast(room, {
    t: 'start', countdown: COUNTDOWN_MS / 1000, course,
    slots: [...room.players.values()].map(p => ({ id: p.id, slot: p.slot })),
  });
  broadcast(room, roomInfo(room));
}

function endRace(room) {
  if (room.phase !== 'racing') return;
  room.phase = 'lobby';
  if (room.isPublic) room.nextStart = Date.now() + PUBLIC_BREAK_MS;
  const unfinished = [...room.players.values()].filter(p => !p.finished)
    .sort((a, b) => ((b.st ? b.st.pr : -1e9) - (a.st ? a.st.pr : -1e9)));
  const list = room.finishOrder.map(p => ({ id: p.id, name: p.name, time: p.time }))
    .concat(unfinished.map(p => ({ id: p.id, name: p.name, time: -1 })));
  const winner = room.finishOrder.length ? room.finishOrder[0].id : 0;
  broadcast(room, { t: 'results', list, winner });
  broadcast(room, roomInfo(room));
}

function checkAllFinished(room) {
  if (room.phase !== 'racing') return;
  const all = [...room.players.values()];
  if (all.length === 0 || all.every(p => p.finished)) endRace(room);
}

function tickRoom(room) {
  const now = Date.now();
  const snap = [];
  for (const p of room.players.values()) if (p.st) snap.push(Object.assign({ id: p.id }, p.st));
  broadcast(room, { t: 'snap', p: snap, next: room.isPublic && room.phase === 'lobby' && room.nextStart ? Math.max(0, (room.nextStart - now) / 1000) : undefined });

  if (room.phase === 'racing') {
    if (room.firstFinish && now - room.firstFinish > FINISH_GRACE_MS) endRace(room);
    else if (now - room.raceStart > RACE_CAP_MS) endRace(room);
  } else if (room.isPublic) {
    if (room.players.size >= 2) {
      if (!room.nextStart) room.nextStart = now + PUBLIC_COUNTDOWN_MS;
      if (now >= room.nextStart) startRace(room);
    } else room.nextStart = 0;
  }
}

function joinRoom(p, room, m, ws) {
  if (room.players.size >= MAX_PLAYERS) { send(ws, { t: 'err', msg: 'That room is full.' }); return; }
  if (room.phase === 'racing') { send(ws, { t: 'err', msg: 'A race is in progress - try again in a moment.' }); return; }
  p.name = cleanName(m.name);
  addPlayer(room, p); send(ws, { t: 'joined', id: p.id }); broadcast(room, roomInfo(room));
}

wss.on('connection', (ws) => {
  const p = { id: nextId++, ws, name: '', room: null };
  ws.isAlive = true;
  ws.on('pong', () => { ws.isAlive = true; });
  ws.on('message', (raw) => {
    let m; try { m = JSON.parse(raw.toString()); } catch { return; }
    if (!m || typeof m.t !== 'string') return;
    switch (m.t) {
      case 'create': {
        if (p.room) return;
        const room = createRoom(p, false);
        if (!room) { send(ws, { t: 'err', msg: 'Could not create a room, try again.' }); return; }
        joinRoom(p, room, m, ws);
        break;
      }
      case 'joinpublic': {
        if (p.room) return;
        joinRoom(p, rooms.get(PUBLIC_CODE) || createRoom(p, true), m, ws);
        break;
      }
      case 'join': {
        if (p.room) return;
        const code = String(m.code || '').toUpperCase().trim();
        if (code === PUBLIC_CODE) { send(ws, { t: 'err', msg: 'Use the quick race button for that one.' }); return; }
        const room = rooms.get(code);
        if (!room) { send(ws, { t: 'err', msg: 'No room with that code.' }); return; }
        joinRoom(p, room, m, ws);
        break;
      }
      case 'start': {
        const room = p.room;
        if (!room || room.isPublic || room.hostId !== p.id || room.phase !== 'lobby') return;
        if (room.players.size < 2) { send(ws, { t: 'err', msg: 'Need at least 2 players to start.' }); return; }
        startRace(room, m.course);
        break;
      }
      case 'state': {
        const room = p.room;
        if (!room || room.phase !== 'racing') return;
        const f = ['x', 'y', 'z', 'h', 'v', 'pr'];
        if (!f.every(k => Number.isFinite(m[k]))) return;
        p.st = { x: m.x, y: m.y, z: m.z, h: m.h, v: m.v, g: m.g | 0, c: m.c | 0, j: m.j | 0, pr: m.pr };
        break;
      }
      case 'finish': {
        const room = p.room;
        if (!room || room.phase !== 'racing' || p.finished) return;
        p.finished = true;
        p.time = Math.max(0, Number(m.time) || (Date.now() - room.raceStart) / 1000);
        room.finishOrder.push(p);
        const first = room.finishOrder.length === 1;
        if (first) { room.firstFinish = Date.now(); p.wins++; }
        broadcast(room, { t: 'finished', id: p.id, name: p.name, first, time: p.time });
        checkAllFinished(room);
        break;
      }
      case 'kick': {
        const room = p.room;
        if (!room || room.isPublic || room.hostId !== p.id) return;
        const target = room.players.get(Number(m.id));
        if (!target || target.id === p.id) return;
        send(target.ws, { t: 'kicked' });
        removePlayer(target);
        break;
      }
      case 'leave': removePlayer(p); break;
    }
  });
  ws.on('close', () => removePlayer(p));
  ws.on('error', () => {});
});

setInterval(() => {
  for (const ws of wss.clients) {
    if (!ws.isAlive) { ws.terminate(); continue; }
    ws.isAlive = false; ws.ping();
  }
}, 20000);

if (require.main === module) {
  server.listen(PORT, () => {
    console.log('Sky Hop Party server listening on port ' + PORT);
    console.log('  On this computer use:  ws://localhost:' + PORT);
  });
}
module.exports = { server, wss, rooms };
