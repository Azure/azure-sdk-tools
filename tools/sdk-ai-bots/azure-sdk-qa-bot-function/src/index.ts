import { app } from '@azure/functions';

// Import function registrations
import './functions/BotAnalytics';
import './functions/ActivityConverter';
import './functions/AdoTokenRefresh';

app.setup({
    enableHttpStream: true,
});
