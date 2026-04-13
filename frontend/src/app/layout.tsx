import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { Toaster } from 'sonner';
import { AuthProvider } from '@/contexts/AuthContext';
import { SignalRProvider } from '@/providers/SignalRProvider';
import { ReactQueryProvider } from '@/providers/ReactQueryProvider';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: { default: 'CoreAr ERP', template: '%s | CoreAr ERP' },
  description: 'Sistema de Gestão para Autoridades de Registro — Certificação Digital ICP-Brasil',
};

/**
 * Ordem dos Provider é crítica:
 * ReactQuery → Auth (precisa do QueryClient) → SignalR (precisa do Auth)
 */
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" className="dark">
      <body className={`${inter.className} bg-gray-950 text-white antialiased`}>
        <ReactQueryProvider>
          <AuthProvider>
            <SignalRProvider>
              {children}

              {/* Toaster do Sonner: dark mode, canto inferior direito */}
              <Toaster
                theme="dark"
                position="bottom-right"
                richColors
                closeButton
                toastOptions={{
                  style: {
                    background: '#111827',
                    border: '1px solid rgba(255,255,255,0.08)',
                    color: '#f9fafb',
                    borderRadius: '12px',
                    fontSize: '14px',
                  },
                }}
              />
            </SignalRProvider>
          </AuthProvider>
        </ReactQueryProvider>
      </body>
    </html>
  );
}
