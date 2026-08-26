import { useEffect, useRef, useState, useCallback } from 'react';
import { ArrowLeft } from 'lucide-react';
import { Game } from './game/game';
import type { HudState, ToastKind } from './game/types';
import LevelDesigner from './pages/LevelDesigner';
import './App.css';

interface Toast {
  id: number;
  msg: string;
  kind: ToastKind;
}

const initialHud: HudState = {
  started: false, paused: false, over: false, won: false, endReason: '',
  prompt: '', channelProgress: -1, carrying: null, disguise: null, crouching: false,
  hasMop: false, hasBlueprint: false, blueprintSent: false, alert: false, timeLeft: 360,
  maxSuspicion: 0, beingWatched: false, objectives: [], stats: { bonks: 0, hides: 0, reports: 0, disguises: 0, cleans: 0 },
};

let toastId = 0;

function fmtTime(s: number): string {
  const m = Math.floor(s / 60);
  const r = Math.floor(s % 60);
  return `${m}:${r.toString().padStart(2, '0')}`;
}

function PrototypeGame({ onBack }: { onBack: () => void }) {
  const mountRef = useRef<HTMLDivElement>(null);
  const gameRef = useRef<Game | null>(null);
  const [hud, setHud] = useState<HudState>(initialHud);
  const [toasts, setToasts] = useState<Toast[]>([]);

  const addToast = useCallback((msg: string, kind: ToastKind) => {
    const id = ++toastId;
    setToasts(prev => [...prev.slice(-4), { id, msg, kind }]);
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 5200);
  }, []);

  useEffect(() => {
    if (!mountRef.current || gameRef.current) return;
    const game = new Game(mountRef.current);
    game.onHud = setHud;
    game.onToast = addToast;
    game.init();
    gameRef.current = game;
    return () => {
      game.dispose();
      gameRef.current = null;
    };
  }, [addToast]);

  return (
    <div className="app-root">
      <div ref={mountRef} className="game-mount" />
      <button className="game-back-button" onClick={onBack} title="Return to workshop"><ArrowLeft size={16} /> Workshop</button>

      {hud.started && !hud.over && !hud.paused && <div className="crosshair">+</div>}
      {hud.started && !hud.over && (hud.beingWatched || hud.alert) && <div className={`vignette ${hud.alert ? 'vignette-alert' : ''}`} />}

      {hud.started && !hud.over && (
        <>
          <div className="panel objectives"><div className="panel-title">SHIFT OBJECTIVES</div>{hud.objectives.map((o, i) => <div key={i} className={`objective ${o.done ? 'done' : ''}`}>{o.done ? '[DONE]' : '[ ]'} {o.label}</div>)}</div>
          <div className={`panel timer ${hud.timeLeft < 60 ? 'timer-low' : ''}`}>TIME {fmtTime(hud.timeLeft)}</div>
          {hud.alert && <div className="alert-banner">SECURITY ALERT / BRIGGS IS COMING</div>}
          <div className="panel suspicion"><div className="panel-title">SUSPICION {hud.beingWatched && <span className="watched-tag">WATCHED</span>}</div><div className="sus-track"><div className="sus-fill" style={{ width: `${hud.maxSuspicion}%`, background: hud.maxSuspicion > 66 ? '#c95345' : hud.maxSuspicion > 33 ? '#bd9147' : '#4b967c' }} /></div></div>
          <div className="chips">{hud.carrying && <div className="chip chip-warn">Carrying: {hud.carrying}</div>}{hud.disguise && <div className="chip chip-good">Disguised as {hud.disguise}</div>}{hud.crouching && <div className="chip">Sneaking</div>}{hud.hasMop && <div className="chip chip-good">Mop equipped</div>}{hud.hasBlueprint && <div className="chip chip-good">Blueprints on you</div>}</div>
          {(hud.prompt || hud.channelProgress >= 0) && <div className="prompt">{hud.prompt}{hud.channelProgress >= 0 && <div className="channel-track"><div className="channel-fill" style={{ width: `${hud.channelProgress * 100}%` }} /></div>}</div>}
        </>
      )}

      <div className="toasts">{toasts.map(t => <div key={t.id} className={`toast toast-${t.kind}`}>{t.msg}</div>)}</div>

      {!hud.started && <div className="overlay"><div className="card"><h1>OFFICE SHIFT</h1><h2>Internal Affairs</h2><p className="tagline">You are embedded at OmniCore Industries. Steal the blueprints, mail them out, and leave no clean story behind.</p><div className="controls-grid"><span><b>WASD</b> walk</span><span><b>Mouse</b> look</span><span><b>E</b> interact</span><span><b>F</b> bonk</span><span><b>Q</b> carry / drop</span><span><b>C</b> sneak</span></div><button className="big-btn" onClick={() => gameRef.current?.start()}>CLOCK IN</button></div></div>}
      {hud.paused && <div className="overlay overlay-clickable" onClick={() => gameRef.current?.resume()}><div className="card"><h1>SHIFT PAUSED</h1><p>Click anywhere to resume.</p></div></div>}
      {hud.over && <div className="overlay"><div className={`card ${hud.won ? 'card-win' : 'card-lose'}`}><h1>{hud.won ? 'SHIFT COMPLETE' : 'BUSTED'}</h1><p className="tagline">{hud.endReason}</p><div className="stats-grid"><div><b>{hud.stats.bonks}</b><span>coworkers bonked</span></div><div><b>{hud.stats.hides}</b><span>bodies filed</span></div><div><b>{hud.stats.cleans}</b><span>stains mopped</span></div><div><b>{hud.stats.disguises}</b><span>identities borrowed</span></div><div><b>{hud.stats.reports}</b><span>reports to security</span></div></div><button className="big-btn" onClick={() => window.location.reload()}>NEXT SHIFT</button></div></div>}
    </div>
  );
}

export default function App() {
  const [mode, setMode] = useState<'designer' | 'game'>('designer');
  return mode === 'designer' ? <LevelDesigner onPlay={() => setMode('game')} /> : <PrototypeGame onBack={() => setMode('designer')} />;
}
