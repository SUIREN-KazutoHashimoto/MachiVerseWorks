export interface ClientPerformanceSnapshot {
  readonly decodeSampleCount: number;
  readonly decodedBytes: number;
  readonly decodeAverageMs: number;
  readonly decodeMaximumMs: number;
  readonly frameSampleCount: number;
  readonly frameAverageMs: number;
  readonly frameMaximumMs: number;
}

export class ClientPerformanceMetrics {
  private decodeSampleCount = 0;
  private decodedBytes = 0;
  private decodeTotalMs = 0;
  private decodeMaximumMs = 0;
  private frameSampleCount = 0;
  private frameTotalMs = 0;
  private frameMaximumMs = 0;
  private previousFrameAt: number | null = null;

  public recordDecode(frameBytes: number, durationMs: number): void {
    if (!Number.isInteger(frameBytes) || frameBytes < 0) {
      throw new RangeError('Decoded frame byte length must be a non-negative integer.');
    }
    if (!Number.isFinite(durationMs) || durationMs < 0) {
      throw new RangeError('Decode duration must be finite and non-negative.');
    }

    this.decodeSampleCount += 1;
    this.decodedBytes += frameBytes;
    this.decodeTotalMs += durationMs;
    this.decodeMaximumMs = Math.max(this.decodeMaximumMs, durationMs);
  }

  public recordAnimationFrame(now: number): void {
    if (!Number.isFinite(now)) {
      throw new RangeError('Animation frame timestamp must be finite.');
    }

    const previous = this.previousFrameAt;
    this.previousFrameAt = now;
    if (previous === null) {
      return;
    }

    const durationMs = now - previous;
    if (durationMs < 0) {
      return;
    }

    this.frameSampleCount += 1;
    this.frameTotalMs += durationMs;
    this.frameMaximumMs = Math.max(this.frameMaximumMs, durationMs);
  }

  public snapshot(): ClientPerformanceSnapshot {
    return {
      decodeSampleCount: this.decodeSampleCount,
      decodedBytes: this.decodedBytes,
      decodeAverageMs: average(this.decodeTotalMs, this.decodeSampleCount),
      decodeMaximumMs: this.decodeMaximumMs,
      frameSampleCount: this.frameSampleCount,
      frameAverageMs: average(this.frameTotalMs, this.frameSampleCount),
      frameMaximumMs: this.frameMaximumMs,
    };
  }
}

function average(total: number, count: number): number {
  return count === 0 ? 0 : total / count;
}
