# Claude 액션링

마우스 휠 클릭으로 원형 메뉴를 띄워 파일 검색·정리·메모·캡처·Claude 물어보기를 쓰는 Windows 데스크톱 도구.

## 깃허브에 올리는 법 (한 번만)

1. 이 폴더(`ClaudeRing`)의 **내용 전체**를 새 GitHub 저장소에 올린다.
   - 예: 저장소 이름을 `ClaudeRing` 으로 만들고 이 파일들을 그대로 push.
2. 저장소 **Settings → Pages** 에서 Pages를 켠다.
   - Source: `Deploy from a branch`, Branch: `main` / 폴더 `/ (root)`.
3. 1~2분 뒤 공개 주소가 생긴다:
   - 소개·다운로드 페이지: `https://<아이디>.github.io/ClaudeRing/`
   - 업데이트용: `https://<아이디>.github.io/ClaudeRing/version.json`
4. 쓰는 사람은 소개 페이지에서 **설치 파일 내려받기** → `설치.bat` 실행.
5. 자동 업데이트를 받으려면, 각자 액션링 **설정 → 일반 → 업데이트 → 업데이트 주소** 에
   `https://<아이디>.github.io/ClaudeRing/version.json` 을 넣는다.

## 새 버전을 낼 때마다

1. 새 `ClaudeRing.cs` 로 교체한다. (첫 줄의 `ClaudeRing-Source` 표시는 그대로 둔다)
2. `version.json` 의 `version` 을 올리고(예: `0.9.0` → `0.9.1`), `notes` 에 바뀐 내용을 적는다.
3. (새로 설치하는 사람용) `ClaudeRing_설치.zip` 안의 `ClaudeRing.cs` 도 같이 바꿔 다시 압축해 올리면 좋다.
4. push → 끝. 쓰는 사람 앱이 켤 때·30분마다 확인해서 자동으로 업데이트한다.

## 들어 있는 파일

| 파일 | 역할 |
|---|---|
| `index.html` | 소개·다운로드 페이지 (GitHub Pages가 보여줌) |
| `version.json` | 최신 버전/변경내용 — 앱이 이걸 보고 업데이트 판단 |
| `ClaudeRing.cs` | 최신 소스 — 앱이 받아서 다시 빌드 |
| `ClaudeRing_설치.zip` | 새로 설치하는 사람용 (설치.bat · 제거.bat 포함) |
