import { useId } from 'react';

interface MarkProps {
  size?: number;
  tile?: boolean;
}

/** The Arclight mark: a geometric A whose crossbar is an arc of light. */
export function LogoMark({ size = 28, tile = false }: MarkProps) {
  const gradientId = useId();
  return (
    <svg width={size} height={size} viewBox={tile ? '0 0 64 64' : '8 8 48 48'} aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#8B5CF6" />
          <stop offset="1" stopColor="#38BDF8" />
        </linearGradient>
      </defs>
      {tile && <rect width="64" height="64" rx="14" fill="#09090B" />}
      <path d="M18.1 42A16 16 0 0 1 45.9 42" fill="none" stroke={`url(#${gradientId})`} strokeWidth="4.5" strokeLinecap="round" />
      <path d="M13.5 52L32 12L50.5 52" fill="none" stroke="#F5F5F7" strokeWidth="6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export function Logo() {
  return (
    <span className="logo">
      <LogoMark />
      <span className="logo__word">Arclight</span>
    </span>
  );
}
