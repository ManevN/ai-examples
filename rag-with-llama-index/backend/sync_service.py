import os
import time
import json
import hashlib
import sync_service
import config
from rag_service import RAGService

def get_file_hash(file_path: str) -> str:
    """Computes the SHA-256 hash of a file's content."""
    hasher = hashlib.sha256()
    with open(file_path, 'rb') as f:
        buf = f.read()
        hasher.update(buf)
    return hasher.hexdigest()

def sync_data_directory():
    print("Starting data synchronization job...")

    rag = RAGService()

    # Load the manifest of currently indexed files
    manifest = {}
    if os.path.exists(config.INDEX_MANIFEST_FILE):
        with open(config.INDEX_MANIFEST_FILE, 'r') as f:
            manifest = json.load(f)
    
    # Get the current state of files in the data directory
    current_files = {}
    for filename in os.listdir(config.DATA_DIR):
        file_path = os.path.join(config.DATA_DIR, filename)
        if os.path.isfile(file_path) and filename.endswith(".txt"):
            current_files[file_path] = get_file_hash(file_path)
    
    indexed_files = set(manifest.keys())
    disk_files = set(current_files.keys())  

    # --- Step 1: Handle deleted files ---
    deleted_files = indexed_files - disk_files
    for file_path in deleted_files:
        rag.delete_document(file_path)
        del manifest[file_path]  

    # --- Step 2: Handle new or modified files ---
    for file_path, file_hash in current_files.items():
        # Check if the file is new or if its content has changed
        if file_path not in manifest or manifest[file_path] != file_hash:
            rag.refresh_document(file_path)
            manifest[file_path] = file_hash

    # --- Step 3: Persist changes ---
    if deleted_files or (disk_files - indexed_files) or any(current_files[fp] != manifest.get(fp) for fp in disk_files):
        rag.persist_index()
        # Save the updated manifest
        with open(config.INDEX_MANIFEST_FILE, 'w') as f:
            json.dump(manifest, f, indent=4)
        print("Data synchronization complete. Changes were made.")
    else:
        print("Data synchronization complete. No changes detected.")

    print("Data synchronization job completed.")

if __name__ == "__main__":
    # Run the sync job once immediately on startup
    sync_data_directory()
