# Kế hoạch tích hợp SAM (Segment Anything Model)

> **Mục tiêu**: Backend .NET gọi được SAM Python chạy trên Colab để xử lý segmentation (tách vùng ảnh) theo click của người dùng.

---

## ✅ Đã hoàn thành (không cần làm lại)

| Hạng mục | File |
|----------|------|
| SAM models (EmbeddingResponse, PredictRequest, MaskResponse) | `src/MangaERP.Api/Models/Sam/SamModels.cs` |
| SAM HTTP client (.NET gọi Python) | `src/MangaERP.Api/Services/SamServiceClient.cs` |
| API Controller (2 endpoint) | `src/MangaERP.Api/Controllers/SegmentationController.cs` |
| Đăng ký DI + timeout 180s | `src/MangaERP.Api/Program.cs` |
| Config placeholder | `src/MangaERP.Api/appsettings.json` |
| Unit tests (4 test cases) | `tests/MangaERP.Api.Tests/SamServiceTests.cs` |
| Build pass (0 errors) | ✅ Confirmed |

---

## 📋 Việc cần làm — theo thứ tự

---

### PHASE 1 — Push code lên Git
**Thời gian**: ~2 phút

```bash
git add .
git commit -m "feat(segmentation): add SAM service client, controller and unit tests"
git push origin bao
```

---

### PHASE 2 — Chuẩn bị ngrok (1 lần duy nhất)
**Thời gian**: ~3 phút

1. Vào [ngrok.com](https://ngrok.com) → **Sign up** bằng Google
2. Vào [dashboard.ngrok.com/get-started/your-authtoken](https://dashboard.ngrok.com/get-started/your-authtoken)
3. Copy token (dạng: `2abc123xyz_XXXXXXXXXX...`)
4. Giữ token này — dùng ở Phase 3

> [!NOTE]
> Chỉ cần đăng ký 1 lần. Token không thay đổi.

---

### PHASE 3 — Chạy SAM Server trên Google Colab
**Thời gian**: ~10-15 phút (chủ yếu download model)

#### Bước 3.1 — Mở Colab + bật GPU
- Vào [colab.research.google.com](https://colab.research.google.com) → tạo notebook mới
- **Bắt buộc**: `Runtime → Change runtime type → T4 GPU → Save`

#### Bước 3.2 — Cell 1: Cài đặt
```python
!pip install segment-anything fastapi uvicorn python-multipart pyngrok numpy pillow -q

# vit_b: 375MB — nhẹ hơn vit_h 6x, đủ dùng cho T4 free tier
!wget -q https://dl.fbaipublicfiles.com/segment_anything/sam_vit_b_01ec64.pth
print("✅ Download xong — model: sam_vit_b (375MB)")
```

#### Bước 3.3 — Cell 2: Viết SAM server
```python
%%writefile sam_service.py
from fastapi import FastAPI, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List
import numpy as np
from PIL import Image
import io, base64, torch
from segment_anything import sam_model_registry, SamPredictor

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
sam = sam_model_registry["vit_b"](checkpoint="sam_vit_b_01ec64.pth")
sam.to(device=DEVICE)
predictor = SamPredictor(sam)
print(f"[SAM] Model loaded on {DEVICE} ✅")

app = FastAPI(title="SAM Service")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_methods=["*"], allow_headers=["*"],
)

class PredictBody(BaseModel):
    embedding: str
    shape: List[int]
    dtype: str
    imageSize: List[int]
    x: float
    y: float

@app.get("/health")
def health():
    return {"status": "ok", "device": DEVICE}

@app.post("/embedding")
async def embedding(file: UploadFile = File(...)):
    contents = await file.read()
    image = np.array(Image.open(io.BytesIO(contents)).convert("RGB"))
    predictor.set_image(image)
    emb = predictor.get_image_embedding().cpu().numpy()
    return {
        "embedding": base64.b64encode(emb.tobytes()).decode(),
        "shape":     list(emb.shape),
        "dtype":     str(emb.dtype),
        "imageSize": [image.shape[1], image.shape[0]]
    }

@app.post("/predict")
async def predict(body: PredictBody):
    dtype = np.dtype(body.dtype)
    emb   = np.frombuffer(base64.b64decode(body.embedding), dtype=dtype).reshape(body.shape)
    predictor.features      = torch.from_numpy(emb).to(DEVICE)
    predictor.original_size = (body.imageSize[1], body.imageSize[0])
    predictor.input_size    = (body.imageSize[1], body.imageSize[0])
    predictor.is_image_set  = True   # ← quan trọng: thiếu dòng này SAM throw exception
    masks, scores, _ = predictor.predict(
        point_coords=np.array([[body.x, body.y]]),
        point_labels=np.array([1]),
        multimask_output=True
    )
    best = int(np.argmax(scores))
    mask = masks[best]
    rows, cols = np.where(mask)
    bbox = (
        [int(cols.min()), int(rows.min()), int(cols.max()), int(rows.max())]
        if len(rows) > 0 else [0, 0, 0, 0]
    )
    return {"maskRle": mask.tolist(), "score": float(scores[best]), "bbox": bbox}
```

#### Bước 3.4 — Cell 3: Chạy server + lấy URL
```python
from pyngrok import ngrok, conf
import subprocess, threading, time

NGROK_TOKEN = "PASTE_TOKEN_CỦA_BẠN_VÀO_ĐÂY"   # ← lấy từ Phase 2
conf.get_default().auth_token = NGROK_TOKEN

threading.Thread(
    target=lambda: subprocess.run(["uvicorn", "sam_service:app", "--host", "0.0.0.0", "--port", "8000"]),
    daemon=True
).start()
time.sleep(5)

url = ngrok.connect(8000).public_url
print(f"✅ Health:     {url}/health")
print(f"📌 Embedding: {url}/embedding")
print(f"📌 Predict:   {url}/predict")
print(f'\n👉 Copy vào appsettings.json → "SamService:Url": "{url}"')
```

#### Bước 3.5 — Cell 4: Xác nhận GPU trước khi dùng
```python
import requests
r = requests.get(f"{url}/health")
assert r.json()["device"] == "cuda", "❌ GPU chưa bật! Restart runtime → T4 GPU"
print(f"✅ SAM đang chạy trên GPU: {r.json()['device']}")
```

> [!CAUTION]
> Nếu Cell 4 báo `"device": "cpu"` → **DỪNG LẠI**, restart Colab và chọn T4 GPU trước khi tiếp tục.

---

### PHASE 4 — Kết nối .NET với Colab
**Thời gian**: ~2 phút

Mở `src/MangaERP.Api/appsettings.json`, cập nhật URL:

```json
"SamService": {
  "Url": "https://xxxx-xxxx.ngrok-free.app"
}
```

Nếu deploy trên **Render/Railway**, thêm environment variable:
```
SamService__Url=https://xxxx-xxxx.ngrok-free.app
```

---

### PHASE 5 — Test end-to-end qua Swagger
**Thời gian**: ~5 phút

#### Test 1 — Lấy embedding
```
POST /api/segmentation/embedding
Authorization: Bearer {access_token_admin}
Content-Type: multipart/form-data
file: [chọn ảnh PNG/JPEG nhỏ ~500x500px]
```

Response mong đợi:
```json
{
  "embedding": "base64string...",
  "shape": [1, 256, 64, 64],
  "dtype": "float32",
  "imageSize": [500, 375]
}
```

#### Test 2 — Dự đoán mask
```
POST /api/segmentation/predict
Authorization: Bearer {access_token_admin}
Content-Type: application/json

{
  "embedding": "...(copy từ Test 1)...",
  "shape": [1, 256, 64, 64],
  "dtype": "float32",
  "imageSize": [500, 375],
  "x": 250,
  "y": 187
}
```

Response mong đợi:
```json
{
  "maskRle": [[false, true, true, ...]],
  "score": 0.94,
  "bbox": [120, 80, 380, 300]
}
```

---

## ⚠️ Lưu ý quan trọng

| Vấn đề | Cách xử lý |
|--------|-----------|
| URL ngrok thay đổi sau mỗi lần restart Colab | Copy URL mới → cập nhật `SamService:Url` |
| Colab tự ngắt sau ~90 phút không dùng | Chạy lại từ Cell 3 để lấy URL mới |
| `/embedding` lần đầu chậm (~30-60s) | Bình thường (SAM warm up). Timeout đã set 180s |
| Colab thu hồi GPU | Đợi vài giờ hoặc dùng Colab Pro |

---

## 🔄 Mỗi lần restart Colab (quy trình nhanh)

```
1. Mở notebook Colab cũ
2. Runtime → Run all   (chạy Cell 1 → 4)
3. Copy URL ngrok mới từ output Cell 3
4. Cập nhật SamService:Url trong config / env var
```
