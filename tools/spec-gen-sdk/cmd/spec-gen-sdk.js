#!/usr/bin/env node
import("../dist/cli/cli.js")
  .then(() => {
    console.log('CLI module loaded successfully.');
  })
  .catch(err => {
    console.error('Error loading CLI module:', err);
  });
