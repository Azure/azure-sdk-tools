import base64
import sys


def main():
    if len(sys.argv) != 2:
        print("Usage: python decode_qr_code.py <base64_string>")
        sys.exit(1)

    b64_string = sys.argv[1]
    try:
        img_bytes = base64.b64decode(b64_string)
    except Exception as e:
        print(f"Error decoding base64: {e}")
        sys.exit(1)

    with open("qrcode.png", "wb") as f:
        f.write(img_bytes)
    print("Decoded image written to qrcode.png")


if __name__ == "__main__":
    main()
