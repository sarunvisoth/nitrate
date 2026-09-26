import type { EntityCapability } from "$lib/api/generated/model";

export type ArtShape = "wide" | "video" | "square" | "portrait" | "poster";

export function artDimensions(shape: ArtShape): { width: number; height: number } {
  switch (shape) {
    case "poster":
      return { width: 720, height: 1080 };
    case "portrait":
      return { width: 810, height: 1080 };
    case "square":
      return { width: 900, height: 900 };
    case "wide":
      return { width: 1260, height: 540 };
    case "video":
    default:
      return { width: 960, height: 540 };
  }
}

export function svgArt(label: string, primary: string, secondary: string, accent: string, shape: ArtShape): string {
  const { width, height } = artDimensions(shape);
  const safeLabel = label.replace(/[<>&"]/g, "");
  const textWidth = Math.min(width - 96, 420);
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}"><defs><linearGradient id="g" x1="0" x2="1" y1="0" y2="1"><stop stop-color="${primary}"/><stop offset="1" stop-color="${secondary}"/></linearGradient><filter id="grain"><feTurbulence baseFrequency=".8" numOctaves="2" stitchTiles="stitch"/><feColorMatrix type="saturate" values="0"/></filter></defs><rect width="${width}" height="${height}" fill="url(#g)"/><rect width="${width}" height="${height}" opacity=".13" filter="url(#grain)"/><path d="M${width * 0.07} ${height * 0.82} C${width * 0.22} ${height * 0.55} ${width * 0.3} ${height * 0.66} ${width * 0.43} ${height * 0.4} S${width * 0.7} ${height * 0.22} ${width * 0.93} ${height * 0.48}" fill="none" stroke="${accent}" stroke-width="${Math.max(16, width * 0.018)}" opacity=".72"/><circle cx="${width * 0.78}" cy="${height * 0.25}" r="${Math.min(width, height) * 0.14}" fill="${accent}" opacity=".34"/><rect x="48" y="48" width="${textWidth}" height="70" fill="#050505" opacity=".5"/><text x="72" y="96" fill="#f4efe6" font-family="Inter,Arial,sans-serif" font-size="36" font-weight="700">${safeLabel}</text></svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

export function rating(value: number): EntityCapability {
  return { kind: "rating", value };
}

export function flags(options: { isNsfw?: boolean; isFavorite?: boolean; isOrganized?: boolean } = {}): EntityCapability {
  return {
    kind: "flags",
    isFavorite: options.isFavorite ?? null,
    isNsfw: options.isNsfw ?? null,
    isOrganized: options.isOrganized ?? null,
  };
}

export function stats(items: Array<{ code: string; value: number }>): EntityCapability {
  return { kind: "stats", items };
}

export function technical(options: {
  duration?: string;
  width?: number;
  height?: number;
  frameRate?: number;
  bitRate?: number;
  sampleRate?: number;
  channels?: number;
  codec?: string;
  container?: string;
  format?: string;
}): EntityCapability {
  return {
    kind: "technical",
    duration: options.duration ?? null,
    width: options.width ?? null,
    height: options.height ?? null,
    frameRate: options.frameRate ?? null,
    bitRate: options.bitRate ?? null,
    sampleRate: options.sampleRate ?? null,
    channels: options.channels ?? null,
    codec: options.codec ?? null,
    container: options.container ?? null,
    format: options.format ?? null,
  };
}

export function positions(items: Array<{ code: string; value: number; label: string }>): EntityCapability {
  return { kind: "position", items };
}
