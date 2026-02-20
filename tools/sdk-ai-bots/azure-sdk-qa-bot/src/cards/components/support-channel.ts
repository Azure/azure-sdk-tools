export const supportChannelCard = {
  type: 'AdaptiveCard',
  $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
  // Currently Microsoft Teams doesn't fully support version 1.6, use 1.5 to ensure compatibility
  version: '1.5',
  body: [],
  actions: [
    {
      type: 'Action.OpenUrl',
      title: 'TypeSpec Discussion',
      url: 'https://teams.microsoft.com/l/channel/19%3A906c1efbbec54dc8949ac736633e6bdf%40thread.skype/TypeSpec%20Discussion?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
    {
      type: 'Action.OpenUrl',
      title: 'Javascript',
      url: 'https://teams.microsoft.com/l/channel/19%3A344f6b5b36ba414daa15473942c7477b%40thread.skype/Language%20%E2%80%93%20JS%E2%80%89%EF%BC%86%E2%80%89TS%20%F0%9F%A5%B7?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
    {
      type: 'Action.OpenUrl',
      title: '.Net',
      url: 'https://teams.microsoft.com/l/channel/19%3A7b87fb348f224b37b6206fa9d89a105b%40thread.skype/Language%20-%20DotNet?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
    {
      type: 'Action.OpenUrl',
      title: 'Java',
      url: 'https://teams.microsoft.com/l/channel/19%3A5e673e41085f4a7eaaf20823b85b2b53%40thread.skype/Language%20-%20Java?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
    {
      type: 'Action.OpenUrl',
      title: 'Python',
      url: 'https://teams.microsoft.com/l/channel/19%3Ab97d98e6d22c41e0970a1150b484d935%40thread.skype/Language%20-%20Python?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
    {
      type: 'Action.OpenUrl',
      title: 'Go',
      url: 'https://teams.microsoft.com/l/channel/19%3A104f00188bb64ef48d1b4d94ccb7a361%40thread.skype/Language%20-%20Go?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
    {
      type: 'Action.OpenUrl',
      title: 'Engineering System',
      url: 'https://teams.microsoft.com/l/channel/19%3A59dbfadafb5e41c4890e2cd3d74cc7ba%40thread.skype/Engineering%20System%20%F0%9F%9B%A0%EF%B8%8F?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47',
    },
  ],
};
