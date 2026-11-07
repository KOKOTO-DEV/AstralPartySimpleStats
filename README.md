# AstralPartySimpleStats<br />
<br />
아스트랄파티 글로벌 Steam INT판의 참가자 통계를 표시하는 독립 BepInEx 플러그인입니다.<br />
<br />

```text
승률: 88.26% (203/230) · 좋아요: 218.6 (656)
매너: 100
```

<br />
- 승률: 총 승리/총 플레이 × 100, 소수 둘째 자리 반올림<br />
- 좋아요: 전체 좋아요 ÷ 3을 소수 첫째 자리에서 버림, 괄호 안은 전체 좋아요<br />
- 매너: 방 참가자 CreditInfo의 차감점수를 100에서 뺀 값, 범위 0~100<br />
- 비공개 통계는 `비공개`, 전달되지 않은 정보는 `확인 불가`<br />
<br />
대기실·캐릭터 선택 화면의 프로필 카드 상단에 살짝 겹쳐 표시합니다.<br />
글자는 왼쪽 정렬이며, 상자 오른쪽 끝을 고정하고 내용 길이에 따라 왼쪽으로 확장합니다.<br />
대기실은 말풍선 버튼을 피하는 오른쪽 여백을 유지하고, 캐릭터 선택창은 카드 왼쪽 끝에 맞춰 오른쪽으로 확장합니다.<br />
<br />
<br />
<img width="1919" height="1078" alt="Image" src="https://github.com/user-attachments/assets/f4bd2c9d-90cd-433f-98df-1139ae4110ff" /><br />
대기실에서 표시<br />
<br />
<br />
<img width="1919" height="1079" alt="Image" src="https://github.com/user-attachments/assets/7951e158-6e35-44fe-a4be-9959d9557e39" /><br />
캐릭터 선택창에서 표시<br />
<br />
<br />
게임의 기존 프로필 요청으로 한 번에 한 명씩 최소 2초 간격으로 자동 조회하며 60초 동안 재사용합니다.<br />
프로필 창은 열지 않고, 수동 프로필 창이 열려 있으면 새 요청을 멈춥니다.<br />
요청 객체별로 응답을 구분해서 자동 조회가 수동 프로필 창 데이터를 덮어쓰지 않습니다.<br />
통계 데이터는 별도 채팅 서버로 전송하지 않습니다.<br />
<br />
<br />
<img width="969" height="417" alt="Image" src="https://github.com/user-attachments/assets/ab56be98-7473-4bb7-9cc8-b42e9ec388b4" /><br />
매너점수가 100점이 아닌 경우도 제대로 표시합니다.<br />
<br />
<br />
<img width="661" height="171" alt="Image" src="https://github.com/user-attachments/assets/c45acbf1-9180-41cf-acc2-6a9e18d05bf5" /><br />
플러그인에서 직접 각각의 유저를 빠르게 조회하기 때문에 이 화면 상태면 잠깐만 기다리면 다시 조회합니다. <br />
<br />
<img width="327" height="79" alt="image" src="https://github.com/user-attachments/assets/2f6734cf-aec3-4520-b408-834510f9abfd" /><br />
비공개는 확인 불가가 아니라 비공개라고 나옵니다.<br />
<br />
승률은 단순히 승리회수 / 전체 게임수를 나눈거라 대략적으로 이렇구나 정도로만 참고하시길 바랍니다.<br />
<br />
좋아요 지수는 전체 좋아요 수를 3으로 나눈 수치입니다.<br />
3인 플레이 같은걸 고려하지 않고 단순히 공개정보에서 3으로 나눈거라 이것도 참고만 하시길 바랍니다.<br />
대략 게임을 200판 했는데 좋아요 지수가 100도 안되면 복귀유저거나 아님 이상한 사람이거나 그렇겠죠.<br />
<br />
<br />
<img width="1098" height="335" alt="Image" src="https://github.com/user-attachments/assets/c18b4567-b66f-4657-9c35-84c04e12de10" /><br />
<img width="645" height="162" alt="image" src="https://github.com/user-attachments/assets/e69cdb84-f99a-4ed1-be68-1b37713d2c90" /><br />
v0.3.0<br />
상세보기의 협동 -> PvE로 변경<br />
상세보기의 PvP 패배의 경우 2위~4위로 표기합니다.<br />
친구 등록 / 유저 차단 버튼이 추가 되었습니다.<br />
현재 방 유저만 10분동안 캐시하고, 참가자 퇴장 시 해당 캐시 삭제, 방 퇴장시에는 전체 삭제되게 하였습니다.<br />
<br />
v0.2.0<br />
유저정보 최근 10회 승패 표시가 나오게 추가 되었습니다.<br />
상세보기 버튼을 누르면 최신순으로 모드, 승패, 사용캐릭터를 표시합니다.<br />
유저정보 표시 자체를 숨기거나 보이게 할 수 있습니다.<br />
캐릭터명을 이름으로 표시하거나 이명으로 표시할 수 있습니다.<br />
<br />
<br />
## 설치<br />
<br />
Windows x64 글로벌 Steam INT판과 BepInEx 6 Unity IL2CPP가 필요합니다.<br />
게임 종료 후 배포 ZIP의 `BepInEx` 폴더를 INT 게임 폴더에 합칩니다.<br />
<br />

```text
8vJXnINT/BepInEx/plugins/AstralPartySimpleStats/AstralPartySimpleStats.dll
```
<br />
설치 DLL은 하나이며 조회용 HybridCLR 브리지는 DLL 안에 내장되어 있습니다.<br />
<br />
<br />
## 설정과 제거<br />
<br />
첫 실행 후 `BepInEx/config/astral-party.simple-stats.cfg`가 생성됩니다.<br />
`[General] Enabled = false`로 표시와 새 자동 조회를 끌 수 있습니다.<br />
삭제하려면 게임 종료 후 `AstralPartySimpleStats` 폴더를 제거합니다.<br />
<br />
<br />
## 라이선스<br />
MIT. <br />
원본 저작권 고지는 LICENSE에 유지했습니다. 배포물에는 게임 DLL을 포함하지 않습니다.

