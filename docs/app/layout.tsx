import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { Provider } from '@/components/provider';
import './global.css';

const inter = Inter({
  subsets: ['latin'],
});

export const metadata: Metadata = {
  metadataBase: new URL('https://aerafaldev.github.io/Palforge'),
  title: {
    default: 'Palforge',
    template: '%s · Palforge',
  },
  description:
    'A clean-room .NET 10 modding runtime for Palworld dedicated servers — plugins, typed hooks, chat commands, and live game state.',
};

export default function Layout({ children }: LayoutProps<'/'>) {
  return (
    <html lang="en" className={inter.className} suppressHydrationWarning>
      <body className="flex flex-col min-h-screen">
        <Provider>{children}</Provider>
      </body>
    </html>
  );
}
