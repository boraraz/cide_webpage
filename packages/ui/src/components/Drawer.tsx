import { X } from 'lucide-react';
import { useEffect, useId, useRef, type ReactNode } from 'react';
import { cx } from '../lib/cx';

export interface DrawerProps {
  open: boolean;
  onClose: () => void;
  title: string;
  side?: 'left' | 'right';
  children: ReactNode;
  className?: string;
}

/**
 * Modal side panel built on the native <dialog> element, which provides focus trapping,
 * Escape handling and an inert background. Used for mobile navigation and, later, for
 * filter, sort and cart panels.
 */
export function Drawer({ open, onClose, title, side = 'left', children, className }: DrawerProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          event.preventDefault();
          onClose();
        }
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
      className={cx(
        'fixed inset-y-0 m-0 h-dvh max-h-dvh w-[min(22rem,88vw)] max-w-none bg-background p-0 text-foreground shadow-lg backdrop:bg-black/40',
        side === 'left' ? 'left-0' : 'right-0 left-auto',
        className,
      )}
    >
      <div className="flex h-full flex-col pt-[env(safe-area-inset-top)] pb-[env(safe-area-inset-bottom)]">
        <div className="flex items-center justify-between border-b border-border px-4">
          <h2 id={titleId} className="text-sm font-medium tracking-editorial uppercase">
            {title}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="-mr-2 inline-flex size-11 items-center justify-center"
          >
            <X aria-hidden="true" className="size-5" strokeWidth={1.5} />
            <span className="sr-only">Close</span>
          </button>
        </div>
        <div className="flex-1 overflow-y-auto overscroll-contain">{children}</div>
      </div>
    </dialog>
  );
}
