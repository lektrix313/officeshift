import { useEffect, useRef, useState, useCallback } from 'react';
import { Game } from './game/game';
import type { HudState, ToastKind } from './game/types';
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

export default function App() {
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

  const restart = () => window.location.reload();

  return (
    <div className="app-root">
      <div ref={mountRef} className="game-mount" />

      {/* crosshair */}
      {hud.started && !hud.over && !hud.paused && <div className="crosshair">+</div>}

      {/* being-watched vignette */}
      {hud.started && !hud.over && (hud.beingWatched || hud.alert) && (
        <div className={`vignette ${hud.alert ? 'vignette-alert' : ''}`} />
      )}

      {hud.started && !hud.over && (
        <>
          {/* objectives */}
          <div className="panel objectives">
            <div className="panel-title">🕴️ SHIFT OBJECTIVES</div>
            {hud.objectives.map((o, i) => (
              <div key={i} className={`objective ${o.done ? 'done' : ''}`}>
                {o.done ? '✅' : '⬜'} {o.label}
              </div>
            ))}
          </div>

          {/* timer */}
          <div className={`panel timer ${hud.timeLeft < 60 ? 'timer-low' : ''}`}>
            ⏱️ {fmtTime(hud.timeLeft)}
          </div>

          {/* alert banner */}
          {hud.alert && <div className="alert-banner">🚨 SECURITY ALERT — BRIGGS IS COMING 🚨</div>}

          {/* suspicion meter */}
          <div className="panel suspicion">
            <div className="panel-title">👁️ SUSPICION {hud.beingWatched && <span className="watched-tag">WATCHED</span>}</div>
            <div className="sus-track">
              <div
                className="sus-fill"
                style={{
                  width: `${hud.maxSuspicion}%`,
                  background: hud.maxSuspicion > 66 ? '#ff3b30' : hud.maxSuspicion > 33 ? '#ffd23a' : '#39d97a',
                }}
              />
            </div>
          </div>

          {/* status chips */}
          <div className="chips">
            {hud.carrying && <div className="chip chip-warn">🧍 Carrying: {hud.carrying} (dripping 🩸)</div>}
            {hud.disguise && <div className="chip chip-good">👔 Disguised as {hud.disguise}</div>}
            {hud.crouching && <div className="chip">🦆 Sneaking</div>}
            {hud.hasMop && <div className="chip chip-good">🧹 Mop equipped</div>}
            {hud.hasBlueprint && <div className="chip chip-good">📁 Blueprints on you</div>}
          </div>

          {/* interact prompt */}
          {(hud.prompt || hud.channelProgress >= 0) && (
            <div className="prompt">
              {hud.prompt}
              {hud.channelProgress >= 0 && (
                <div className="channel-track">
                  <div className="channel-fill" style={{ width: `${hud.channelProgress * 100}%` }} />
                </div>
              )}
            </div>
          )}
        </>
      )}

      {/* toast log */}
      <div className="toasts">
        {toasts.map(t => (
          <div key={t.id} className={`toast toast-${t.kind}`}>{t.msg}</div>
        ))}
      </div>

      {/* start screen */}
      {!hud.started && (
        <div className="overlay">
          <div className="card">
            <h1>🕴️ OFFICE SHIFT</h1>
            <h2>Internal Affairs</h2>
            <p className="tagline">You are an industrial spy embedded at OmniCore Industries.<br />
              Steal the blueprints. Mail them out. Blame Karen from accounting.</p>
            <div className="controls-grid">
              <span><b>WASD</b> — walk around innocently</span>
              <span><b>Mouse</b> — look shifty</span>
              <span><b>E</b> — interact / hold to steal</span>
              <span><b>F</b> — bonk coworker ⌨️</span>
              <span><b>Q</b> — pick up / drop body</span>
              <span><b>C</b> — sneak</span>
            </div>
            <p className="fineprint">Bonking is messy: blood splatters, bodies leak when carried, and anyone who spots
              evidence will wander over (❓), panic (😱), and squeal to security when their countdown hits zero —
              unless you mop the stain, hide the body, or bonk the witness. The mop lives in the supply closet. 🧹</p>
            <button className="big-btn" onClick={() => gameRef.current?.start()}>
              CLOCK IN ⏰
            </button>
          </div>
        </div>
      )}

      {/* pause screen */}
      {hud.paused && (
        <div className="overlay overlay-clickable" onClick={() => gameRef.current?.resume()}>
          <div className="card">
            <h1>⏸️ SMOKE BREAK</h1>
            <p>Click anywhere to get back to "work".</p>
          </div>
        </div>
      )}

      {/* end screen */}
      {hud.over && (
        <div className="overlay">
          <div className={`card ${hud.won ? 'card-win' : 'card-lose'}`}>
            <h1>{hud.won ? '🏆 SHIFT COMPLETE' : '🚔 BUSTED'}</h1>
            <p className="tagline">{hud.endReason}</p>
            <div className="stats-grid">
              <div><b>{hud.stats.bonks}</b><span>coworkers bonked</span></div>
              <div><b>{hud.stats.hides}</b><span>bodies "filed"</span></div>
              <div><b>{hud.stats.cleans}</b><span>blood stains mopped</span></div>
              <div><b>{hud.stats.disguises}</b><span>identities borrowed</span></div>
              <div><b>{hud.stats.reports}</b><span>reports to security</span></div>
            </div>
            <button className="big-btn" onClick={restart}>NEXT SHIFT 🔄</button>
          </div>
        </div>
      )}
    </div>
  );
}
