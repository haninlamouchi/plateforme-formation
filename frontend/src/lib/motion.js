/**
 * Shared animation presets.
 * Import these instead of repeating the same values across components.
 *
 * Usage:
 *   import { fadeUp, stagger, spring } from '../lib/motion';
 *   <motion.div variants={fadeUp} />
 */

export const fadeUp = {
  initial: { opacity: 0, y: 16 },
  animate: {
    opacity: 1,
    y: 0,
    transition: { type: 'spring', stiffness: 280, damping: 22 },
  },
};

export const stagger = {
  animate: {
    transition: { staggerChildren: 0.07, delayChildren: 0.1 },
  },
};

export const spring = { type: 'spring', stiffness: 260, damping: 22 };

export const springFast = { type: 'spring', stiffness: 320, damping: 28 };
