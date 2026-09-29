import { app } from '@azure/functions';

// Import function registrations
import './functions/AdoTokenRefresh';

app.setup({
    enableHttpStream: true,
});
