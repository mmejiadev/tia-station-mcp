import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App.tsx';
import { LiveProvider } from './live.tsx';
import './styles.css';
import { applyStoredTheme, ThemeProvider } from './theme.tsx';

const root = document.getElementById('root');

// Refused rather than asserted with a `!`. If the element is missing the page is broken, and a stack
// trace saying which element is missing is the only thing that says so.
if (root === null) {
  throw new Error('index.html has no element with id "root", so there is nowhere to render.');
}

// Before the first render, not in an effect after it: the charts read their colours out of the
// document, and a class applied later leaves them painted in the other theme's palette.
applyStoredTheme();

createRoot(root).render(
  <StrictMode>
    <ThemeProvider>
      <LiveProvider>
        <App />
      </LiveProvider>
    </ThemeProvider>
  </StrictMode>
);
