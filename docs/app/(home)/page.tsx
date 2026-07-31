import Link from 'next/link';

export default function HomePage() {
  return (
    <main className="flex flex-1 flex-col items-center justify-center text-center px-4">
      <h1 className="text-3xl font-bold sm:text-4xl">Palforge</h1>
      <p className="mt-3 max-w-xl text-fd-muted-foreground">
        A clean-room .NET 10 modding runtime for Palworld dedicated servers. Write plugins in plain C# —
        hook native engine functions, add chat commands, and read and write live game state against a
        typed, generated SDK, with no baked offsets.
      </p>
      <div className="mt-6 flex gap-3">
        <Link
          href="/docs"
          className="rounded-lg bg-fd-primary px-4 py-2 text-sm font-medium text-fd-primary-foreground"
        >
          Read the docs
        </Link>
        <a
          href="https://github.com/AerafalDev/Palforge"
          className="rounded-lg border px-4 py-2 text-sm font-medium"
        >
          GitHub
        </a>
      </div>
    </main>
  );
}
