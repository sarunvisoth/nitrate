/** A short, non-blocking message shown over a reader, with an optional single action. */
export interface ReaderNotice {
  message: string;
  actionLabel?: string;
  onAction?: () => void;
  onDismiss: () => void;
}
