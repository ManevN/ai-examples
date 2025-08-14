
import streamlit as st
import requests

st.set_page_config(page_title="Chat UI", page_icon=":speech_balloon:", layout="centered")
st.markdown("<h1 style='text-align: center;'>💬 Chat with RAG Service</h1>", unsafe_allow_html=True)

if "chat_history" not in st.session_state:
    st.session_state["chat_history"] = []

API_URL = "http://localhost:5000/chat"  # Updated to match backend

chat_container = st.container()

with chat_container:
    for sender, message in st.session_state["chat_history"]:
        if sender == "You":
            st.markdown(f"""
                <div style='text-align: right; background-color: #22223b; color: #fff; padding: 12px; border-radius: 12px; margin: 6px 0; box-shadow: 0 2px 8px rgba(34,34,59,0.08);'>
                    <b>You:</b> {message}
                </div>
            """, unsafe_allow_html=True)
        else:
            st.markdown(f"""
                <div style='text-align: left; background-color: #22223b; color: #fff; padding: 12px; border-radius: 12px; margin: 6px 0; box-shadow: 0 2px 8px rgba(34,34,59,0.08);'>
                    <b>Ani-Farm:</b> {message}
                </div>
            """, unsafe_allow_html=True)

st.markdown("<hr>", unsafe_allow_html=True)

col1, col2 = st.columns([5,1])
with col1:
    user_message = st.text_input("Type your message:", "", key="user_input")
with col2:
    send_clicked = st.button("Send", use_container_width=True)

if send_clicked and user_message:
    st.session_state["chat_history"].append(("You", user_message))
    try:
        response = requests.post(API_URL, json={"prompt": user_message})
        chat_response = response.json().get("response", "No response")
    except Exception as e:
        chat_response = f"Error: {e}"
    st.session_state["chat_history"].append(("Bot", chat_response))
    st.rerun()
