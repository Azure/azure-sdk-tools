#!/bin/bash

PID_FILE="service.pid"
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
        echo "Usage: $0 {start|stop|restart|status}"
        exit 1
        ;;
esac

exit 0
