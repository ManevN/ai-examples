import os
import config
from flask import Flask, request, jsonify

# Initialize Flask app
app = Flask(__name__)

@app.route('/chat', methods=['POST'])
def chat_endpoint():
    user_input = request.get_json()
    response = {"reply": f"You said: {user_input}"}
    return jsonify(response)

@app.route("/feedback", methods=["POST"])
def feedback_endpoint():
    feedback = request.get_json()
    response = {"status": "success", "data": feedback}
    return jsonify(response)

@app.route('/health', methods=['GET'])
def health_check():
    return jsonify({"status": "ok", "message": "Application is up and running."})

if __name__ == '__main__':
    # Ensure log and storage directories exist
    os.makedirs(config.LOG_DIR, exist_ok=True)
    os.makedirs(config.STORAGE_DIR, exist_ok=True)
    
    app.run(debug=True)
