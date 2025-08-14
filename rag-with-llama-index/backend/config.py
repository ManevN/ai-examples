import os
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

# --- LLM and Embedding Model ---
# Fetches the OpenAI API key from environment variables
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
# Specifies the LLM to be used
LLM_MODEL = "gpt-4o-mini"
# Specifies the Hugging Face model for generating embeddings
EMBED_MODEL = "BAAI/bge-small-en-v1.5"

# --- RAG Configuration ---
# The size of text chunks for indexing
CHUNK_SIZE = 512
# The amount of overlap between adjacent chunks
CHUNK_OVERLAP = 64

# --- File Paths ---
# Directory where source documents are stored
DATA_DIR = "./backend/files/data"
# Directory to persist the vector index
STORAGE_DIR = "./backend/files/storage"
# Directory for log files
LOG_DIR = "./backend/files/logs"
# Path for the audit log CSV file
AUDIT_LOG_FILE = os.path.join(LOG_DIR, "audit_log.csv")
# Path for the index state manifest (tracks file versions)
INDEX_MANIFEST_FILE = os.path.join(STORAGE_DIR, "index_manifest.json")