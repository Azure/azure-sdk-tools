# Use the official Azure Functions Node.js base image
FROM mcr.microsoft.com/azure-functions/node:4-node22

# Install git and other necessary tools
RUN apt-get update && \
    apt-get install -y git curl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Set the working directory
WORKDIR /home/site/wwwroot

# Copy package.json and package-lock.json (if available)
COPY package*.json ./

# Install npm dependencies
RUN npm install

# Copy the rest of the application code
COPY . .

# Build the TypeScript code
RUN npm run build

# Expose the port (Azure Functions typically use port 80)
EXPOSE 80

# The base image already sets up the proper entrypoint for Azure Functions
