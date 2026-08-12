# Use the official Azure Functions Node.js runtime as the base image
# Pin to the latest v4 Node 22 LTS image for security patches
FROM mcr.microsoft.com/azure-functions/node:4-node22-appservice

# Set the working directory inside the container
WORKDIR /home/site/wwwroot

# Apply security patches and install git
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y git && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy package.json and package-lock.json (if available)
COPY package*.json ./

# Install dependencies
RUN npm install

# Copy the rest of the application code
COPY . .

# Build the TypeScript code
RUN npm run build

# Set required environment variables for Node.js Functions runtime
ENV FUNCTIONS_WORKER_RUNTIME=node
ENV AzureWebJobsFeatureFlags=EnableWorkerIndexing

# Expose the port that Azure Functions uses
EXPOSE 80

# The base image already contains the Azure Functions runtime
# and will automatically start the function host
