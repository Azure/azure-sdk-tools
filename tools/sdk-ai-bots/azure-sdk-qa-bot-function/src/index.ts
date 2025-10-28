import { app } from '@azure/functions';

// Import function registrations
import './functions/BotAnalytics';
import './functions/ActivityConverter';

app.setup({
    enableHttpStream: true,
});
