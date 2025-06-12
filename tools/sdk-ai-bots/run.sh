#!/bin/bash

GO_SERVICE_DIR="azure-sdk-qa-bot-backend"
SHARED_SERVICE_DIR="azure-sdk-qa-bot-backend-shared"
PID_FILE="service.pid"
SHARED_PID_FILE="shared_service.pid"
DEV_MODE=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        start|stop|restart|status)
            COMMAND=$1
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 {start|stop|restart|status}"
            exit 1
            ;;
    esac
done

start_service() {
    if [ -f "$PID_FILE" ]; then
        echo "Go service appears to be already running. PID file exists."
        return 1
    fi
    
    # Start the Go application from the correct directory
    cd "$GO_SERVICE_DIR" || { echo "Error: Cannot change to Go service directory"; return 1; }
    echo "Starting Go service from $(pwd)..."
    nohup go run . > ../service.log 2>&1 &
    cd ..
    
    # Wait briefly for the actual service to start
    sleep 2
    # Get the PID of the actual service process listening on port 8088
    SERVICE_PID=$(lsof -ti:8088)
    if [ ! -z "$SERVICE_PID" ]; then
        echo $SERVICE_PID > "$PID_FILE"
        echo "Started Go service with PID: $SERVICE_PID"
    else
        echo "Warning: Could not find service PID listening on port 8088"
    fi
    
    # Start the shared service (always run this)
    if [ -f "$SHARED_PID_FILE" ]; then
        echo "Shared service appears to be already running. Shared PID file exists."
    else
        echo "Starting shared service from $SHARED_SERVICE_DIR..."
        cd "$SHARED_SERVICE_DIR" || { echo "Error: Cannot change to shared service directory"; return 1; }
        nohup npm run dev:local > ../shared_service.log 2>&1 &
        SHARED_PID=$!
        cd ..
        echo $SHARED_PID > "$SHARED_PID_FILE"
        echo "Started shared service with PID: $SHARED_PID"
    fi
}

stop_service() {
    # Stop Go service
    if [ -f "$PID_FILE" ]; then
        echo "Stopping Go service..."
        SERVICE_PID=$(cat "$PID_FILE")
        # Also try to find any process listening on port 8088
        PORT_PID=$(lsof -ti:8088)
        if [ ! -z "$SERVICE_PID" ]; then
            kill $SERVICE_PID 2>/dev/null
        fi
        if [ ! -z "$PORT_PID" ] && [ "$PORT_PID" != "$SERVICE_PID" ]; then
            kill $PORT_PID 2>/dev/null
        fi
        rm "$PID_FILE"
    else
        # Try to stop by port if PID file doesn't exist
        PORT_PID=$(lsof -ti:8088)
        if [ ! -z "$PORT_PID" ]; then
            kill $PORT_PID 2>/dev/null
        fi
        echo "Go service PID file not found, tried stopping by port"
    fi
    
    # Stop shared service
    if [ -f "$SHARED_PID_FILE" ]; then
        echo "Stopping shared service..."
        SHARED_PID=$(cat "$SHARED_PID_FILE")
        kill $SHARED_PID 2>/dev/null
        # Kill any potential child processes
        pkill -P $SHARED_PID 2>/dev/null
        rm "$SHARED_PID_FILE"
    else
        echo "Shared service PID file not found"
    fi
}

status_service() {
    PORT_PID=$(lsof -ti:8088)
    if [ -f "$PID_FILE" ]; then
        STORED_PID=$(cat "$PID_FILE")
        if [ ! -z "$PORT_PID" ]; then
            if [ "$PORT_PID" = "$STORED_PID" ]; then
                echo "Go service is running with PID: $PORT_PID"
            else
                echo "Go service is running with PID: $PORT_PID (PID file shows: $STORED_PID)"
            fi
        else
            echo "No process found listening on port 8088"
        fi
    else
        if [ ! -z "$PORT_PID" ]; then
            echo "Go service is running with PID: $PORT_PID (no PID file)"
        else
            echo "Go service is not running"
        fi
    fi
    
    # Check shared service status
    if [ -f "$SHARED_PID_FILE" ]; then
        SHARED_PID=$(cat "$SHARED_PID_FILE")
        if kill -0 $SHARED_PID 2>/dev/null; then
            echo "Shared service is running with PID: $SHARED_PID"
        else
            echo "Shared service PID file exists but process is not running"
        fi
    else
        echo "Shared service is not running"
    fi
}

case "$COMMAND" in
    start)
        start_service
        ;;
    stop)
        stop_service
        ;;
    restart)
        stop_service
        sleep 2
        start_service
        ;;
    status)
        status_service
        ;;
    *)
        echo "Usage: $0 [-dev] {start|stop|restart|status}"
        exit 1
        ;;
esac

exit 0
