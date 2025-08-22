import json
import io
from pathlib import Path
from typing import Dict, Any, List, Tuple, Optional, Union

import streamlit as st


# ------------------------- Helpers -------------------------

def load_docstore_json(src: Optional[Union[io.BytesIO, str, Path]]) -> Dict[str, Any]:
    """Load a docstore.json either from an uploaded file or a filesystem path."""
    if src is None:
        return {}
    if hasattr(src, "read"):  # Uploaded file-like (BytesIO)
        data = src.read()
        return json.loads(data.decode("utf-8"))
    p = Path(src)
    with p.open("r", encoding="utf-8") as f:
        return json.load(f)


def group_roots_and_nodes(docstore: Dict[str, Any]) -> Tuple[Dict[str, Any], Dict[str, Any], Dict[str, Any]]:
    """Return the three main sections we care about."""
    meta = docstore.get("docstore/metadata", {}) or {}
    ref = docstore.get("docstore/ref_doc_info", {}) or {}
    data = docstore.get("docstore/data", {}) or {}
    return meta, ref, data


def get_node_payload(node_obj: Dict[str, Any]) -> Dict[str, Any]:
    """Return the payload for a node, accounting for version differences."""
    return node_obj.get("__data__", {}) or {}


def get_node_text(payload: Dict[str, Any]) -> str:
    """Extract the text content from a node payload (handles common variants)."""
    return (
        payload.get("text")
        or payload.get("document", {}).get("text")
        or payload.get("raw_text")
        or ""
    )


def decode_relationships(rels: Dict[str, Any]) -> List[Dict[str, str]]:
    """
    Best-effort decoding of relationship types.
    Common persisted mapping: 1=PARENT, 2=PREVIOUS, 3=NEXT.
    """
    mapping = {"1": "PARENT", "2": "PREVIOUS", "3": "NEXT"}
    decoded: List[Dict[str, str]] = []
    if not isinstance(rels, dict):
        return decoded
    for code, info in rels.items():
        kind = mapping.get(str(code), "REL_" + str(code))
        node_id = ""
        if isinstance(info, dict):
            node_id = str(info.get("node_id", ""))
        decoded.append({"kind": kind, "node_id": node_id})
    return decoded


def resolve_root_file_path(root_id: str, ref: Dict[str, Any], data: Dict[str, Any]) -> Optional[str]:
    """
    Resolve a root's file path, using ref_doc_info metadata if present; otherwise,
    peek at the first node's metadata for file_path/filepath/path/source.
    """
    info = ref.get(root_id, {}) if isinstance(root_id, str) else {}
    md = info.get("metadata", {}) if isinstance(info, dict) else {}
    fp = None
    for k in ("file_path", "filepath", "path", "source"):
        v = md.get(k)
        if v:
            fp = str(v)
            break
    if fp:
        return fp

    node_ids = info.get("node_ids", []) if isinstance(info, dict) else []
    if isinstance(node_ids, (str, bytes)):
        node_ids = [node_ids]
    for nid in node_ids[:1]:
        payload = get_node_payload(data.get(nid, {}))
        nmd = payload.get("metadata", {}) or {}
        for k in ("file_path", "filepath", "path", "source"):
            v = nmd.get(k)
            if v:
                return str(v)
    return None


def count_nodes_for_root(root_id: str, ref: Dict[str, Any]) -> int:
    node_ids = ref.get(root_id, {}).get("node_ids", [])
    if isinstance(node_ids, (str, bytes)):
        return 1
    return len(node_ids or [])


def find_orphan_nodes(meta: Dict[str, Any], ref: Dict[str, Any]) -> List[str]:
    """
    Nodes that have a ref_doc_id pointing to a root not present in ref_doc_info.
    """
    roots = set(ref.keys())
    orphans: List[str] = []
    for obj_id, m in meta.items():
        if not isinstance(m, dict):
            continue
        r = m.get("ref_doc_id")
        if r and r not in roots:
            orphans.append(obj_id)
    return orphans


def search_match(text: str, query: str) -> bool:
    if not query:
        return True
    q = query.lower().strip()
    return q in (text or "").lower()


# ------------------------- UI -------------------------

st.set_page_config(page_title="LlamaIndex Docstore Viewer", layout="wide")

st.title("📚 LlamaIndex Docstore Viewer")
st.caption("Inspect `docstore.json` → roots, chunks, metadata, relationships, and text (click to expand).")

with st.sidebar:
    st.header("Load docstore.json")
    uploaded = st.file_uploader("Upload docstore.json", type=["json"])
    default_path = st.text_input("...or enter a path", value="docstore.json")

    st.markdown("---")
    st.subheader("Filters")
    query = st.text_input("Search (chunk id or text)", help="Case-insensitive substring search.")
    show_text = st.checkbox("Show text in expanders", True)
    show_metadata = st.checkbox("Show metadata", True)
    show_relationships = st.checkbox("Show relationships", True)

    st.markdown("---")
    st.subheader("Display")
    collapse_all = st.checkbox("Start chunks collapsed", True)
    max_chunks = st.number_input("Max chunks per root to display", min_value=1, max_value=5000, value=200)

# Load data
try:
    docstore = load_docstore_json(uploaded if uploaded else default_path)
except Exception as e:
    st.error(f"Failed to load JSON: {e}")
    st.stop()

meta, ref, data = group_roots_and_nodes(docstore)

# Summary metrics
col1, col2, col3, col4 = st.columns(4)
with col1:
    st.metric("Roots (reference docs)", len(ref))
with col2:
    st.metric("Metadata entries", len(meta))
with col3:
    st.metric("Nodes (in data)", len(data))
with col4:
    st.metric("Orphan nodes", len(find_orphan_nodes(meta, ref)))

st.markdown("---")

# Show each root
if not ref:
    st.info("No `docstore/ref_doc_info` found.")
else:
    for root_id, info in ref.items():
        file_path = resolve_root_file_path(root_id, ref, data) or "Unknown file"
        node_ids = info.get("node_ids", []) or []
        if isinstance(node_ids, (str, bytes)):
            node_ids = [node_ids]

        # Root header block
        st.subheader(f"📄 {file_path}")
        root_cols = st.columns([2, 1, 1, 2])
        with root_cols[0]:
            st.markdown("**Root ID**")
            st.code(str(root_id), language="text")
        with root_cols[1]:
            st.markdown("**Chunks declared**")
            st.write(len(node_ids))
        with root_cols[2]:
            present = sum(1 for nid in node_ids if nid in data)
            st.markdown("**Chunks found in data**")
            st.write(present)
        with root_cols[3]:
            md = (info.get("metadata") or {})
            if md:
                ftype = md.get("file_type", "?")
                fsize = md.get("file_size", "?")
                mod = md.get("last_modified_date", "?")
                st.markdown("**Summary**")
                st.write(f"{ftype} • {fsize} bytes • modified {mod}")

        # List chunks with expanders
        displayed = 0
        for nid in node_ids:
            if displayed >= max_chunks:
                break
            node_obj = data.get(nid)
            payload = get_node_payload(node_obj or {})
            text = get_node_text(payload)

            # Apply search filter (by id or text)
            if not (search_match(nid, query) or search_match(text, query)):
                continue

            title = f"Chunk: {nid}"
            with st.expander(title, expanded=not collapse_all):
                cols = st.columns([2, 2, 1])
                with cols[0]:
                    st.markdown("**Node ID**")
                    st.code(nid, language="text")
                with cols[1]:
                    start = payload.get("start_char_idx")
                    end = payload.get("end_char_idx")
                    st.markdown("**Span**")
                    st.write(f"{start} – {end}" if start is not None else "n/a")
                with cols[2]:
                    st.markdown("**MIME**")
                    st.write(payload.get("mimetype", "n/a"))

                if show_metadata:
                    st.markdown("**Metadata**")
                    st.json(payload.get("metadata", {}) or {})

                if show_relationships:
                    st.markdown("**Relationships**")
                    rels = decode_relationships(payload.get("relationships", {}) or {})
                    if rels:
                        st.table(rels)
                    else:
                        st.write("None")

                if show_text:
                    st.markdown("**Text**")
                    if text:
                        st.text(text)
                    else:
                        st.info("No text field found in this node payload.")

            displayed += 1

        if displayed == 0:
            st.warning("No chunks match the current filter for this document.")

# Orphans section (if any)
orphans = find_orphan_nodes(meta, ref)
if orphans:
    st.markdown("---")
    st.subheader("⚠️ Orphan Nodes (ref_doc_id not present in ref_doc_info)")
    for nid in orphans:
        st.code(nid, language="text")
