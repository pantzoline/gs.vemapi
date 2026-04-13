/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
    './src/providers/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      keyframes: {
        // Animação do sininho quando há notificações não lidas
        wiggle: {
          '0%, 100%': { transform: 'rotate(-8deg)' },
          '25%':       { transform: 'rotate(8deg)' },
          '50%':       { transform: 'rotate(-5deg)' },
          '75%':       { transform: 'rotate(5deg)' },
        },
        // Animação de entrada suave para toasts e dropdowns
        'slide-in-from-top-2': {
          '0%': { transform: 'translateY(-8px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
      },
      animation: {
        wiggle: 'wiggle 1s ease-in-out 3', // Executa 3 vezes e para
      },
    },
  },
  plugins: [
    require('tailwindcss-animate'), // Para animate-in / fade-in / zoom-in do Shadcn
  ],
};
