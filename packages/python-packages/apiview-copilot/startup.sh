#!/bin/bash

# Install git
apt-get update && apt-get install -y git

# Start the application
gunicorn -w 4 -b 0.0.0.0:8000 app:app