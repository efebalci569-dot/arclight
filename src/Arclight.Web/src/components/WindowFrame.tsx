interface WindowFrameProps {
  src: string;
  alt: string;
  eager?: boolean;
}

/** A screenshot of the real app, framed like the Arclight window it came from. */
export function WindowFrame({ src, alt, eager = false }: WindowFrameProps) {
  return (
    <figure className="window">
      <img src={src} alt={alt} width={1440} height={900} loading={eager ? 'eager' : 'lazy'} decoding="async" />
    </figure>
  );
}
