import os

import openai
import config
from rag_service import RAGService
from audit_logger import log_query, log_feedback
from flask import Flask, request, jsonify

# Initialize Flask app
app = Flask(__name__)

# Initialize the RAG service on application startup
# This ensures models are loaded only once
try:
    rag_service = RAGService()
except Exception as e:
    print(f"Fatal error during RAG Service initialization: {e}")
    rag_service = None

@app.route('/chat', methods=['POST'])
def chat_endpoint():
    data = request.get_json()
    prompt = data['prompt']
    response_text = rag_service.query(prompt)
    log_id = log_query(prompt, response_text)
    
    return jsonify({
        "response": response_text,
        "log_id": log_id
    })

@app.route("/feedback", methods=["POST"])
def feedback_endpoint():
    data = request.get_json()
    log_id = data.get('log_id')
    feedback_value = data.get('feedback')
    if not log_id or not feedback_value:
        return jsonify({"error": "Missing 'log_id' or 'feedback' in request body"}), 400

    if log_feedback(log_id, feedback_value):
        return jsonify({"status": "success", "message": "Feedback recorded."})
    else:
        return jsonify({"error": "Invalid feedback value or log_id not found."}), 400

@app.route('/health', methods=['GET'])
def health_check():
    return jsonify({"status": "ok", "message": "Application is up and running."})

@app.route('/health/openai', methods=['GET'])
def health_openai():
    try:
        client = openai.OpenAI(api_key=config.OPENAI_API_KEY)
        client.models.list()
        return jsonify({"status": "ok", "message": "OpenAI API is accessible."})
    except openai.AuthenticationError:
        return jsonify({"status": "error", "details": "OpenAI API authentication failed. Check your API key."}), 500
    except Exception as e:
        return jsonify({"status": "error", "details": str(e)}), 500

if __name__ == '__main__':
    os.makedirs(config.LOG_DIR, exist_ok=True)
    os.makedirs(config.STORAGE_DIR, exist_ok=True)
    
    app.run(debug=True)
