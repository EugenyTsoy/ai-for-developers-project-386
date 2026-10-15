import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    server: {
        port: 49567,
        proxy: {
            '/api': {
                target: process.env.API_BASE_URL || 'http://localhost:5200',
                changeOrigin: true,
            },
        },
    }
})
