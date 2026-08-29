const TIMING_WINDOW_SIZE = 240;

export interface ClientPerformanceSnapshot {
  readonly decodeSampleCount: number;
  readonly decodedBytes: number;
  readonly decodeAverageMs: number;
  readonly decodeP95Ms: number;
  readonly decodeMaximumMs: number;
  readonly frameSampleCount: number;
  readonly frameAverageMs: number;
  readonly frameP95Ms: number;
  readonly frameMaximumMs: number;
}

export class ClientPerformanceMetrics {
  private readonly decodeTimings = new RollingTimingWindow(TIMING_WINDOW_SIZE);
  private readonly frameTimings = new RollingTimingWindow(TIMING_WINDOW_SIZE);
  private decodeSampleCount = 0;
  private decodedBytes = 0;
  private frameSampleCount = 0;
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
    this.decodeTimings.record(durationMs);
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
    this.frameTimings.record(durationMs);
  }

  public snapshot(): ClientPerformanceSnapshot {
    const decode = this.decodeTimings.snapshot();
    const frame = this.frameTimings.snapshot();
    return {
      decodeSampleCount: this.decodeSampleCount,
      decodedBytes: this.decodedBytes,
      decodeAverageMs: decode.averageMs,
      decodeP95Ms: decode.p95Ms,
      decodeMaximumMs: decode.maximumMs,
      frameSampleCount: this.frameSampleCount,
      frameAverageMs: frame.averageMs,
      frameP95Ms: frame.p95Ms,
      frameMaximumMs: frame.maximumMs,
    };
  }
}

interface TimingWindowSnapshot {
  readonly averageMs: number;
  readonly p95Ms: number;
  readonly maximumMs: number;
}

class RollingTimingWindow {
  private readonly values: Float64Array;
  private count = 0;
  private nextIndex = 0;
  private total = 0;

  public constructor(size: number) {
    this.values = new Float64Array(size);
  }

  public record(value: number): void {
    if (this.count === this.values.length) {
      this.total -= this.values[this.nextIndex] ?? 0;
    } else {
      this.count += 1;
    }

    this.values[this.nextIndex] = value;
    this.total += value;
    this.nextIndex = (this.nextIndex + 1) % this.values.length;
  }

  public snapshot(): TimingWindowSnapshot {
    if (this.count === 0) {
      return { averageMs: 0, p95Ms: 0, maximumMs: 0 };
    }

    const sorted = Array.from(this.values.subarray(0, this.count)).sort((left, right) => left - right);
    const p95Index = Math.max(0, Math.ceil(sorted.length * 0.95) - 1);
    return {
      averageMs: this.total / this.count,
      p95Ms: sorted[p95Index] ?? 0,
      maximumMs: sorted[sorted.length - 1] ?? 0,
    };
  }
}
