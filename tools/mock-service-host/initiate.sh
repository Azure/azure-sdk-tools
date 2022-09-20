#!/bin/sh
SUBSCRIPTION_ID=7fd08dcc-a653-4b0f-8f8c-4dac889fdda4
VAULT_NAME=avskeys

# echo "Refreshing Azure subscription cache..."
# az account list --refresh > /dev/null
echo "Generating .env from Azure KeyVault..."


for i in $(az keyvault secret list --subscription $SUBSCRIPTION_ID --vault-name $VAULT_NAME --query '[*].id' --output tsv | cut -d/ -f5)
do
  echo "Adding secret: $i"
  VALUE=`az keyvault secret show --subscription $SUBSCRIPTION_ID --vault-name $VAULT_NAME --name $i --query 'value' --output tsv`
  echo "-----BEGIN RSA PRIVATE KEY-----" > .ssh/$i".pem"
  for line in ${VALUE//;/ } ; do 
   echo "$line" >> .ssh/$i".pem"
  done
  echo "-----END RSA PRIVATE KEY-----" >> .ssh/$i".pem"
done

echo "Done"