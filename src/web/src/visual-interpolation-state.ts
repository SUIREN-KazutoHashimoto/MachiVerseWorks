export interface VisualPosition {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

interface PositionTrack {
  previous: VisualPosition;
  current: VisualPosition;
  receivedAt: number;
  interpolationDurationMs: number;
}

/**
 * Presentation-only position history. This state must never be treated as
 * authoritative simulation state or sent back to the server.
 */
export class VisualInterpolationState<Id> {
  private readonly tracks = new Map<Id, PositionTrack>();

  public upsert(id: Id, position: VisualPosition, receivedAt: number): void {
    const track = this.tracks.get(id);
    if (track === undefined) {
      this.tracks.set(id, {
        previous: position,
        current: position,
        receivedAt,
        interpolationDurationMs: 100,
      });
      return;
    }

    const observedInterval = receivedAt - track.receivedAt;
    track.previous = track.current;
    track.current = position;
    track.receivedAt = receivedAt;
    if (Number.isFinite(observedInterval) && observedInterval > 0) {
      track.interpolationDurationMs = clamp(observedInterval, 33, 500);
    }
  }

  public sample(id: Id, now: number): VisualPosition | undefined {
    const track = this.tracks.get(id);
    if (track === undefined) return undefined;
    const alpha = clamp((now - track.receivedAt) / track.interpolationDurationMs, 0, 1);
    return {
      x: lerp(track.previous.x, track.current.x, alpha),
      y: lerp(track.previous.y, track.current.y, alpha),
      z: lerp(track.previous.z, track.current.z, alpha),
    };
  }

  public remove(id: Id): boolean { return this.tracks.delete(id); }
  public clear(): void { this.tracks.clear(); }
}

function lerp(from: number, to: number, alpha: number): number { return from + (to - from) * alpha; }
function clamp(value: number, minimum: number, maximum: number): number { return Math.min(maximum, Math.max(minimum, value)); }
