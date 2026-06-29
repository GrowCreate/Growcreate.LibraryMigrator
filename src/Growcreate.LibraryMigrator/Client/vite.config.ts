import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
    },
    outDir: '../wwwroot/App_Plugins/Growcreate.LibraryMigrator',
    // Clear the output dir each build so old hashed chunks don't linger.
    // public/ (incl. umbraco-package.json) is re-copied after emptying, so the manifest survives.
    // Explicit `true` also suppresses Vite's "outDir is outside project root" empty-prompt.
    emptyOutDir: true,
    rollupOptions: {
      external: [/^@umbraco-cms\//],
      output: {
        entryFileNames: 'element-migrator.js',
        chunkFileNames: '[name]-[hash].js',
      },
    },
  },
});
