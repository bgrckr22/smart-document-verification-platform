from pathlib import Path
import sys


SERVICE_ROOT = Path(__file__).resolve().parents[2] / "cv-service"
sys.path.insert(0, str(SERVICE_ROOT))
