import os
import uuid
import pandas as pd
from datetime import datetime
import config

# --- CSV-based Logging Implementation ---

def _get_log_df():
    """Reads the audit log CSV into a pandas DataFrame or creates a new one."""
    if os.path.exists(config.AUDIT_LOG_FILE):
        return pd.read_csv(config.AUDIT_LOG_FILE)
    else:
        # Create the directory if it doesn't exist
        os.makedirs(os.path.dirname(config.AUDIT_LOG_FILE), exist_ok=True)
        # Define columns for the new DataFrame
        return pd.DataFrame(columns=["id", "timestamp", "prompt", "response", "feedback"])
 
def log_query(prompt: str, response: str) -> str:
    """
    Logs a new prompt and its response to the CSV file.

    Args:
        prompt (str): The user's query.
        response (str): The model's answer.

    Returns:
        str: A unique ID for this log entry.
    """
    df = _get_log_df()
    log_id = str(uuid.uuid4())
    new_log = pd.DataFrame([{
        "id": log_id,
        "timestamp": datetime.now().isoformat(),
        "prompt": prompt,
        "response": response,
        "feedback": "N/A" # Default feedback state
    }])

    # Append the new log and save to CSV
    df = pd.concat([df, new_log], ignore_index=True)
    df.to_csv(config.AUDIT_LOG_FILE, index=False)
    
    # log_to_firebase(prompt, response) # Example of calling the Firebase function
    return log_id

def log_feedback(log_id: str, feedback: str) -> bool:
    """
    Updates the feedback for a specific log entry in the CSV file.

    Args:
        log_id (str): The unique ID of the log entry to update.
        feedback (str): The feedback provided by the user ('like' or 'dislike').

    Returns:
        bool: True if the update was successful, False otherwise.
    """
    if feedback not in ["like", "dislike"]:
        return False
        
    df = _get_log_df()
    
    # Find the row with the matching ID
    if log_id in df['id'].values:
        df.loc[df['id'] == log_id, 'feedback'] = feedback
        df.to_csv(config.AUDIT_LOG_FILE, index=False)
        # update_feedback_in_firebase(log_id, feedback) # Example call
        return True
    
    return False