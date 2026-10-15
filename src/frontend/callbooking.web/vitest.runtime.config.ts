import { defineConfig, mergeConfig } from 'vitest/config'
import config from './vitest.config.ts'

export default mergeConfig(config, defineConfig({
  test: {
    include: ['src/test/*.runtime.tsx'],
    testTimeout: 30000,
  },
}))
