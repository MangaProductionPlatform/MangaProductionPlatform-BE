# SAM 3 Google Colab + ngrok + Render Setup Guide

> Cap nhat: 2026-07-09  
> Muc tieu: chay `facebook/sam3` tren Google Colab GPU T4, expose API bang ngrok, va cho backend Render goi qua `SamService__Url`.

---

## 1. Tong quan

Backend hien tai khong goi Hugging Face truc tiep. Backend chi goi mot SAM service HTTP ben ngoai qua:

```text
GET  /health
POST /embedding
POST /predict
```

Vi vay Hugging Face token chi dung trong Google Colab de tai model `facebook/sam3`. Khong dua Hugging Face token len Render va khong commit token vao Git.

Render can:

```text
SamService__Url=https://xxxx.ngrok-free.app
```

Neu muon bao ve endpoint ngrok, them:

```text
SamService__InternalApiKey=<secret-tu-dat>
```

Gia tri nay phai trung voi `SAM_INTERNAL_API_KEY` tren Colab.

---

## 2. Chuan bi

Can co:

- Hugging Face account da duoc cap quyen truy cap `facebook/sam3`.
- Google Colab runtime GPU T4.
- ngrok account va ngrok auth token.
- Render backend service dang co env `SamService__Url`.

Repo ID SAM 3:

```text
facebook/sam3
```

Luu y: repo nay dung `safetensors`, khong can tim `sam3.pt`.

---

## 3. Thu tu cell tren Google Colab

### Cell 1 - Kiem tra GPU

```python
# Cell nay kiem tra Colab da duoc cap GPU chua.
# Neu thay Tesla T4 nghia la runtime GPU da bat dung.
!nvidia-smi
```

Neu chua thay GPU:

```text
Runtime -> Change runtime type -> T4 GPU -> Save
```

---

### Cell 2 - Cai thu vien

```python
# Cell nay cai cac thu vien de:
# - login va tai model tu Hugging Face
# - load SAM 3 bang transformers
# - chay FastAPI server
# - mo public URL bang ngrok
#
# Neu Colab hien popup "Khoi dong lai phien", hay bam restart,
# sau do chay lai tu Cell 1.
!pip install -U huggingface_hub hf_xet transformers accelerate safetensors pillow matplotlib requests fastapi uvicorn pyngrok python-multipart nest_asyncio
```

---

### Cell 3 - Kiem tra login Hugging Face

```python
# Cell nay kiem tra Colab da login Hugging Face chua.
# Neu tra ve thong tin account thi OK.
# Neu bao chua login, chay Cell 4.
from huggingface_hub import whoami

whoami()
```

---

### Cell 4 - Login Hugging Face neu can

```python
# Chi chay cell nay neu whoami() bao chua login.
# Colab se hien link + code.
# Mo link, nhap code, dang nhap Hugging Face va bam authorize.
from huggingface_hub import login

login()
```

Neu OAuth device login thanh cong, `whoami()` se hien account cua ban.

---

### Cell 5 - Kiem tra quyen doc repo SAM 3

```python
# Cell nay kiem tra account Hugging Face co quyen doc repo facebook/sam3 khong.
# Neu in ra danh sach file la quyen da OK.
# Repo facebook/sam3 dung safetensors, khong can tim sam3.pt.
from huggingface_hub import list_repo_files

repo_id = "facebook/sam3"

files = list_repo_files(repo_id=repo_id)

for f in files:
    print(f)
```

Neu gap loi 403/gated repo:

- Kiem tra account Hugging Face da duoc approve `facebook/sam3` chua.
- Kiem tra Colab dang login dung account chua bang `whoami()`.

---

### Cell 6 - Load SAM 3 len GPU

```python
# Cell nay load model SAM 3 image segmentation len GPU.
# Dung Sam3Processor + Sam3Model de tranh load nham processor video.
# Lan dau chay se tai model kha lau. Lan sau nhanh hon vi da cache.
import torch
from transformers import Sam3Processor, Sam3Model

device = "cuda" if torch.cuda.is_available() else "cpu"

model = Sam3Model.from_pretrained("facebook/sam3").to(device)
processor = Sam3Processor.from_pretrained("facebook/sam3")

model.eval()

print(type(processor))
print(type(model))
print("Loaded SAM3 on", device)
```

Ket qua mong doi:

```text
Loaded SAM3 on cuda
```

---

### Cell 7 - Test inference bang anh mau

```python
# Cell nay tai mot anh mau tu COCO dataset va thu segment doi tuong bang text prompt.
# Neu output co Found objects > 0 nghia la SAM 3 inference hoat dong.
from PIL import Image
import requests
import torch

image_url = "http://images.cocodataset.org/val2017/000000077595.jpg"
image = Image.open(requests.get(image_url, stream=True).raw).convert("RGB")

inputs = processor(
    images=image,
    text="cat",
    return_tensors="pt"
).to(device)

with torch.no_grad():
    outputs = model(**inputs)

results = processor.post_process_instance_segmentation(
    outputs,
    threshold=0.5,
    mask_threshold=0.5,
    target_sizes=inputs.get("original_sizes").tolist()
)[0]

print("Found objects:", len(results["masks"]))
print(results.keys())
```

Ket qua mong doi:

```text
Found objects: 1
dict_keys(['scores', 'boxes', 'masks'])
```

---

### Cell 8 - Hien thi mask de kiem tra bang mat

```python
# Cell nay overlay mask SAM 3 len anh goc.
# Vung mau do la vung model detect/segment duoc.
# Dung de kiem tra ket qua co hop ly khong truoc khi mo API cho backend.
import numpy as np
from PIL import Image

def overlay_masks(image, masks):
    image_rgba = image.convert("RGBA")
    masks_np = masks.cpu().numpy().astype(np.uint8)

    for mask in masks_np:
        color = Image.new("RGBA", image_rgba.size, (255, 0, 0, 0))
        alpha = Image.fromarray(mask * 120).resize(image_rgba.size)
        color.putalpha(alpha)
        image_rgba = Image.alpha_composite(image_rgba, color)

    return image_rgba

overlay = overlay_masks(image, results["masks"])
overlay
```

---

### Cell 9 - Tao secret bao ve ngrok endpoint

Cell nay tuy chon nhung nen dung khi gan ngrok URL vao Render.

```python
# Cell nay set khoa bi mat de bao ve API ngrok.
# Ban tu dat gia tri nao cung duoc.
# Render backend phai dung dung cung gia tri nay qua SamService__InternalApiKey.
#
# Neu bo qua cell nay, ngrok URL ai biet cung co the goi duoc.
import os

os.environ["SAM_INTERNAL_API_KEY"] = "sam3-demo-secret-2026"
```

Neu da chay `%%writefile main.py` truoc do thi khong sao. Chi can cell nay duoc chay truoc khi chay `uvicorn`.

Neu da chay `uvicorn` roi moi set key, can stop server va chay lai `uvicorn`.

---

### Cell 10 - Tao file API `main.py`

```python
%%writefile main.py
# File nay tao FastAPI server de backend .NET goi SAM 3 qua HTTP.
#
# Backend hien tai dang goi 3 route:
# GET  /health
# POST /embedding
# POST /predict
#
# Luu y:
# - /embedding o day khong tra embedding that kieu SAM2 cu.
# - No tra base64 anh goc de /predict dung lai.
# - Cach nay giu nguyen contract backend hien tai ma khong phai sua .NET code.
# - /predict dung SAM3 text prompt "object" de segment object tong quat.

import base64
import io
import os
import numpy as np
import torch
from PIL import Image
from fastapi import FastAPI, UploadFile, File, Header, HTTPException
from pydantic import BaseModel
from transformers import Sam3Processor, Sam3Model

app = FastAPI()

# Neu ban muon bao ve ngrok endpoint, set bien moi truong SAM_INTERNAL_API_KEY.
# Neu bien nay rong, API se khong yeu cau key.
INTERNAL_API_KEY = os.environ.get("SAM_INTERNAL_API_KEY", "")

# Load SAM3 khi server start.
device = "cuda" if torch.cuda.is_available() else "cpu"
model = Sam3Model.from_pretrained("facebook/sam3").to(device)
processor = Sam3Processor.from_pretrained("facebook/sam3")
model.eval()

def check_key(x_internal_api_key: str | None):
    # Neu INTERNAL_API_KEY duoc set, request phai gui header X-Internal-Api-Key dung gia tri.
    if INTERNAL_API_KEY and x_internal_api_key != INTERNAL_API_KEY:
        raise HTTPException(status_code=401, detail="Invalid internal API key")

def encode_image_bytes(image_bytes: bytes):
    # Encode anh thanh base64 string de backend giu trong field embedding.
    return base64.b64encode(image_bytes).decode("utf-8")

def decode_image_bytes(image_b64: str):
    # Decode base64 string ve bytes anh de /predict chay SAM3.
    return base64.b64decode(image_b64)

def simple_rle(mask: np.ndarray):
    # Convert binary mask thanh RLE don gian.
    # Backend dang giu maskRle dang object nen format nay co the serialize JSON duoc.
    pixels = mask.astype(np.uint8).flatten(order="F")
    counts = []
    count = 0
    prev = 0

    for pix in pixels:
        if pix == prev:
            count += 1
        else:
            counts.append(count)
            count = 1
            prev = pix

    counts.append(count)

    return {
        "counts": counts,
        "size": list(mask.shape)
    }

@app.get("/health")
def health(x_internal_api_key: str | None = Header(default=None)):
    # Endpoint de backend kiem tra Colab SAM service con song khong.
    check_key(x_internal_api_key)
    return {
        "status": "ok",
        "model": "facebook/sam3",
        "device": device
    }

@app.post("/embedding")
async def embedding(
    file: UploadFile = File(...),
    x_internal_api_key: str | None = Header(default=None)
):
    # Endpoint backend goi sau khi upload anh.
    # Tra ve format tuong thich voi BE hien tai:
    # embedding, shape, dtype, imageSize.
    check_key(x_internal_api_key)

    image_bytes = await file.read()
    image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    width, height = image.size

    return {
        "embedding": encode_image_bytes(image_bytes),
        "shape": [height, width, 3],
        "dtype": "image-bytes",
        "imageSize": [height, width]
    }

class PredictRequest(BaseModel):
    # Model request khop voi backend .NET dang gui sang /predict.
    embedding: str
    shape: list[int]
    dtype: str
    imageSize: list[int]
    x: float
    y: float

@app.post("/predict")
def predict(
    req: PredictRequest,
    x_internal_api_key: str | None = Header(default=None)
):
    # Endpoint backend goi de lay mask.
    # Hien backend chi gui click point x/y, chua gui text prompt.
    # Vi SAM3 manh o text prompt, ban tam dung text="object".
    check_key(x_internal_api_key)

    if req.dtype != "image-bytes":
        raise HTTPException(status_code=400, detail="Unsupported embedding dtype")

    image_bytes = decode_image_bytes(req.embedding)
    image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    width, height = image.size

    inputs = processor(
        images=image,
        text="object",
        return_tensors="pt"
    ).to(device)

    with torch.no_grad():
        outputs = model(**inputs)

    results = processor.post_process_instance_segmentation(
        outputs,
        threshold=0.5,
        mask_threshold=0.5,
        target_sizes=inputs.get("original_sizes").tolist()
    )[0]

    if len(results["masks"]) == 0:
        empty_mask = np.zeros((height, width), dtype=np.uint8)
        return {
            "maskRle": simple_rle(empty_mask),
            "score": 0.0,
            "bbox": [0, 0, 0, 0]
        }

    masks = results["masks"]
    scores = results["scores"]
    boxes = results["boxes"]

    # Chon mask co score cao nhat.
    best_idx = int(torch.argmax(scores).item())

    mask = masks[best_idx].detach().cpu().numpy().astype(np.uint8)
    score = float(scores[best_idx].detach().cpu().item())
    box = boxes[best_idx].detach().cpu().numpy().astype(int).tolist()

    x1, y1, x2, y2 = box

    return {
        "maskRle": simple_rle(mask),
        "score": score,
        "bbox": [x1, y1, x2 - x1, y2 - y1]
    }
```

---

### Cell 11 - Cho phep chay server trong notebook

```python
# Cell nay cho phep uvicorn chay trong moi truong notebook Colab.
import nest_asyncio

nest_asyncio.apply()
```

---

### Cell 12 - Mo ngrok public URL

Nen chay cell nay truoc khi chay server neu muon thay URL som.

```python
# Cell nay mo public URL bang ngrok de Render/backend goi duoc Colab.
# Thay DAN_NGROK_TOKEN_CUA_BAN bang token lay tu ngrok dashboard.
from pyngrok import ngrok

ngrok.set_auth_token("DAN_NGROK_TOKEN_CUA_BAN")

public_url = ngrok.connect(8000)
print(public_url)
```

Ket qua se co dang:

```text
https://xxxx.ngrok-free.app
```

---

### Cell 13 - Chay FastAPI server

```python
# Cell nay chay API server tren port 8000.
# Khi cell dung o log "Uvicorn running on http://0.0.0.0:8000" la dung.
# Dung stop cell nay khi muon backend goi SAM.
!uvicorn main:app --host 0.0.0.0 --port 8000
```

---

## 4. Test ngrok

Mo trinh duyet:

```text
https://xxxx.ngrok-free.app/health
```

Neu khong set `SAM_INTERNAL_API_KEY`, ket qua mong doi:

```json
{
  "status": "ok",
  "model": "facebook/sam3",
  "device": "cuda"
}
```

Neu da set `SAM_INTERNAL_API_KEY`, goi browser truc tiep co the bi `401` vi browser khong gui header `X-Internal-Api-Key`. Luc do de backend Render goi la dung, mien Render co env `SamService__InternalApiKey` trung gia tri.

---

## 5. Cau hinh Render

Trong Render service backend, vao Environment va set:

```text
SamService__Url=https://xxxx.ngrok-free.app
```

Neu co dung secret:

```text
SamService__InternalApiKey=sam3-demo-secret-2026
```

Sau do restart/deploy lai backend:

```text
Render -> Manual Deploy -> Deploy latest commit
```

Khong them `/health`, `/embedding`, hoac `/predict` vao `SamService__Url`.

Dung:

```text
SamService__Url=https://xxxx.ngrok-free.app
```

Sai:

```text
SamService__Url=https://xxxx.ngrok-free.app/health
```

---

## 6. Luu y van hanh

- Moi lan Colab runtime tat, model se can load lai.
- Moi lan ngrok free URL doi, phai cap nhat lai `SamService__Url` tren Render.
- Hugging Face token chi dung tren Colab, khong dua len Render.
- `SAM_INTERNAL_API_KEY` la secret tu dat, khong lien quan Hugging Face.
- `SAM_INTERNAL_API_KEY` tren Colab phai bang `SamService__InternalApiKey` tren Render.
- Neu da set secret sau khi server dang chay, can stop va chay lai `uvicorn`.

---

## 7. Checklist hoan thanh

- [ ] Colab hien GPU `Tesla T4`.
- [ ] `whoami()` hien dung Hugging Face account.
- [ ] `list_repo_files("facebook/sam3")` in duoc danh sach file.
- [ ] SAM 3 load thanh cong voi `Loaded SAM3 on cuda`.
- [ ] Test inference ra `Found objects`.
- [ ] `main.py` duoc tao.
- [ ] ngrok URL duoc tao.
- [ ] `uvicorn` dang chay port `8000`.
- [ ] Render co `SamService__Url` tro toi ngrok URL.
- [ ] Neu dung secret, Render co `SamService__InternalApiKey` trung voi Colab.
- [ ] Render backend da restart/deploy lai.
