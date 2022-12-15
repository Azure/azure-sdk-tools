# engagement-experience

This is the deployment config of the SDK Release App should update.

## SDK release app deployment variable to change

1. environment variable: 'SDK Generation DockerImage', SDK REPO OWNER

2. environment variable: 'SDK Generation Pipeline DefinitionId', SDK generation pipeline id of **'SDK Generation - Trigger SDK Generation Pipeline'** flow

3. Dataverse: SDK Owner e-mail address update

4. Check dataverse table columns length (especially branchName and Url)

5. Check sdk generation pipeline environment variable

6. Edit flow **'SDK Scheduled Release'** change Start time early than today to trigger flow

7. environment variable: 'ApiViewHost', set apiview host address.

## Test SDK release app in PPE env

This is the requirement for testing in PPE environment.

1. environment variable: 'SDK Generation Create ReleaseRequest', don't create ReleaseRequest in test

2. If don't set 'environment variable', go to 'https://github.com/Azure/sdk-release-request/issues/created_by/@me' close
