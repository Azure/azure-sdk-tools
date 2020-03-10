This tool allows the creation and management of the fabric bot tool.

### How to use

 CreateRuleFabricBot.exe create org repo servicesFile [-token value] [-prompt value]
  - org          : The org the repo is in (string, required)
  - repo         : The name of the repo (string, required)
  - servicesFile : The file containing the service table. Structure for the table Labels: Column1, Handles: Column3 (string, required)
  - token        : The cookie token for authentication (string, default=)
  - prompt       : Don't prompt for validation (true or false, default=True)

 CreateRuleFabricBot.exe update org repo servicesFile [-token value] [-prompt value]
  - org          : The org the repo is in (string, required)
  - repo         : The name of the repo (string, required)
  - servicesFile : The file containing the service table. Structure for the table Labels: Column1, Handles: Column3 (string, required)
  - token        : The cookie token for authentication (string, default=)
  - prompt       : Don't prompt for validation (true or false, default=True)

 CreateRuleFabricBot.exe delete org repo task [-token value] [-prompt value]
  - org    : The org the repo is in (string, required)
  - repo   : The name of the repo (string, required)
  - task   : The task id to delete. (string, required)
  - token  : The cookie token for authentication (string, default=)
  - prompt : Don't prompt for validation (true or false, default=True)

 CreateRuleFabricBot.exe deleteall org repo [-token value] [-prompt value]
  - org    : The org the repo is in (string, required)
  - repo   : The name of the repo (string, required)
  - token  : The cookie token for authentication (string, default=)
  - prompt : Don't prompt for validation (true or false, default=True)

 CreateRuleFabricBot.exe listTaskIds org repo [-token value] [-prompt value]
  - org    : The org the repo is in (string, required)
  - repo   : The name of the repo (string, required)
  - token  : The cookie token for authentication (string, default=)
  - prompt : Don't prompt for validation (true or false, default=True)