import { app } from '@azure/functions';

// Import function registrations

app.setup({
    enableHttpStream: true,
});
