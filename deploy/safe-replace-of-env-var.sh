#!/bin/bash
# A script to safely update a key-value pair in a .env file.

# Check if the correct number of arguments is provided.
if [ "$#" -ne 3 ]; then
    echo "Usage: $0 VARIABLE_NAME \"NEW_VALUE\" FILENAME"
    exit 1
fi

# Assign arguments to readable variable names.
VAR_NAME=$1
NEW_VALUE=$2
FILENAME=$3

# Check if the target file exists.
if [ ! -f "$FILENAME" ]; then
    echo "Error: File '$FILENAME' not found."
    exit 1
fi

# Escape the new value to handle special sed characters like /, &, and \.
# This is the core of the robust solution.
ESCAPED_VALUE=$(printf '%s\n' "$NEW_VALUE" | sed -e 's/[\/&]/\\&/g')

# Use sed to find the line starting with VAR_NAME= and replace it.
# The command is in double quotes to allow shell variable expansion.
sed -i "s/^${VAR_NAME}=.*/${VAR_NAME}=${ESCAPED_VALUE}/" "$FILENAME"

echo "Successfully updated '${VAR_NAME}' in '${FILENAME}'."