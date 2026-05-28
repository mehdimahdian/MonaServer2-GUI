import type { Config } from 'tailwindcss'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: {
          DEFAULT: '#0F0F1A',
          card: '#1A1A2E',
          elevated: '#252545',
          border: '#2D2D4E',
        },
        accent: {
          DEFAULT: '#6060C0',
          green: '#22DD88',
          red: '#DD4444',
          blue: '#22AAFF',
          yellow: '#DDCC22',
        },
        text: {
          primary: '#E0E0FF',
          secondary: '#A0A0C0',
          muted: '#6060A0',
        },
      },
      fontFamily: {
        mono: ['Consolas', 'Monaco', 'monospace'],
      },
    },
  },
  plugins: [],
} satisfies Config
