#!/bin/bash

# Configuration
API_URL="http://localhost:5000/api/Papers/sync?size=1000"
MAX_ITERATIONS=10000
CURRENT_ITERATION=1

echo "Starting paper sync process..."
echo "API URL: $API_URL"
echo "Max iterations: $MAX_ITERATIONS"
echo "=================================="

# Loop for the specified number of iterations
while [ $CURRENT_ITERATION -le $MAX_ITERATIONS ]; do
    echo "Iteration $CURRENT_ITERATION of $MAX_ITERATIONS"
    echo "$(date): Starting sync request..."
    
    # Make the API call and capture the response
    RESPONSE=$(curl -s -X 'POST' \
        "$API_URL" \
        -H 'accept: */*' \
        -H 'Content-Type: application/json' \
        -d '' \
        -w "HTTP_STATUS:%{http_code}")
    
    # Extract HTTP status code
    HTTP_STATUS=$(echo $RESPONSE | grep -o "HTTP_STATUS:[0-9]*" | cut -d: -f2)
    RESPONSE_BODY=$(echo $RESPONSE | sed -E 's/HTTP_STATUS:[0-9]*$//')
    
    # Check if the request was successful
    if [ "$HTTP_STATUS" -eq 200 ]; then
        echo "✅ Success (HTTP $HTTP_STATUS)"
        # Extract processed count from response if available
        PROCESSED=$(echo $RESPONSE_BODY | grep -o '"documentsProcessed":[0-9]*' | cut -d: -f2)
        if [ ! -z "$PROCESSED" ]; then
            echo "📄 Documents processed: $PROCESSED"
        fi
    else
        echo "❌ Failed (HTTP $HTTP_STATUS)"
        echo "Response: $RESPONSE_BODY"
        
        # Optional: uncomment the next line to stop on errors
        # break
    fi
    
    echo "$(date): Completed iteration $CURRENT_ITERATION"
    echo "--------------------------------"
    
    # Increment counter
    CURRENT_ITERATION=$((CURRENT_ITERATION + 1))
    
    # Optional: Add a small delay between requests (uncomment if needed)
    # sleep 1
done

echo "=================================="
echo "Paper sync process completed!"
echo "Total iterations: $((CURRENT_ITERATION - 1))"
echo "Finished at: $(date)"
