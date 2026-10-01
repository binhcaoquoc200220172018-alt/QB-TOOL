;;; ==========================================================================
;;; CHUONG TRINH AUTOLISP: XUAT TIM CONG SANG CSV (DV_TOOL_HTKT - PHIEN BAN V2.0)
;;; Tac gia: DV_TOOL_HTKT
;;; Muc dich: Trich xuat toa do VN2000 (X1, Y1, X2, Y2), Chieu dai, Goc xoay
;;;           tu Binh do AutoCAD sang file TOA_DO_CONG.csv
;;; Lenh su dung: XTC hoac XUAT_TIM_CONG (Pick 2 diem dau cong - Khuyen dung)
;;;               XTCL (Click chon duong Line/Polyline tim cong co san)
;;; ==========================================================================

(vl-load-com)

;; Bien toan cuc quan ly file de dam bao luon dong an toan
(setq *xtc_file* nil)

;; Ham xu ly loi va khoi phuc cai dat
(defun xtc_err (msg)
  (if *xtc_file*
    (progn (close *xtc_file*) (setq *xtc_file* nil))
  )
  (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*,*EXIT*")))
    (princ (strcat "\n[Loi]: " msg))
  )
  (if old_cmdecho (setvar "CMDECHO" old_cmdecho))
  (if old_osmode (setvar "OSMODE" old_osmode))
  (setq *error* old_error)
  (princ "\n[Thong bao]: Da thoat lenh an toan.")
  (princ)
)

;; Ham chuyen Radian sang Do (0 - 360)
(defun r2d (rad)
  (/ (* rad 180.0) pi)
)

;; Ham lay thu muc ban ve hien tai
(defun get_csv_dir ()
  (setq dwg_dir (getvar "DWGPREFIX"))
  (if (or (= dwg_dir "") (null dwg_dir))
    (setq dwg_dir "C:\\")
  )
  dwg_dir
)

;; Ham kiem tra va lay duong dan CSV an toan (tranh loi khi Excel dang mo file)
(defun get_safe_csv_path (/ dir def_path f)
  (setq dir (get_csv_dir))
  (setq def_path (strcat dir "TOA_DO_CONG.csv"))
  ;; Kiem tra mo thu file
  (setq f (open def_path "a"))
  (if f
    (progn
      (close f)
      def_path
    )
    (progn
      ;; Neu file bi khoa boi Excel, tao file co gio phut giay
      (setq alt_path (strcat dir "TOA_DO_CONG_" (menucmd "M=$(edtime,$(getvar,date),YYMODD_HHMMSS)") ".csv"))
      (princ (strcat "\n[Luu y]: File TOA_DO_CONG.csv dang bi mo trong Excel! Se luu sang file: " alt_path))
      alt_path
    )
  )
)

;; Ham lam sach chuoi Text / MText / Leader lay Ly Trinh
(defun clean_text_value (raw_str / i len ch res km_pos sub_km)
  (if (or (null raw_str) (= raw_str ""))
    "Cong_X"
    (progn
      (setq len (strlen raw_str))
      (setq res "")
      (setq i 1)
      ;; Loai bo dau phay de khong vo file CSV, loai bo cac ky tu dieu khien \P
      (while (<= i len)
        (setq ch (substr raw_str i 1))
        (cond
          ;; Thay \P (xuong dong MText) bang khoang trang
          ((and (= ch "\\") (<= (1+ i) len) (= (strcase (substr raw_str (1+ i) 1)) "P"))
           (setq res (strcat res " "))
           (setq i (1+ i)))
          ;; Loai bo cac ky tu gay loi CSV hoac ma dinh dang font
          ((or (= ch ",") (= ch ";") (= ch "\"") (= ch "}") (= ch "{"))
           (setq res (strcat res " ")))
          (T
           (setq res (strcat res ch)))
        )
        (setq i (1+ i))
      )
      
      ;; Loc tim cu the doan "Km" neu co de chuoi sieu gon
      (setq km_pos (vl-string-search "KM" (strcase res)))
      (if km_pos
        (progn
          (setq sub_km (substr res (1+ km_pos)))
          (vl-string-trim " " sub_km)
        )
        (vl-string-trim " " res)
      )
    )
  )
)

;; Ham doc Text tu doi tuong duoc chon (ho tro TEXT, MTEXT, LEADER)
(defun get_text_from_entity (ent / ed typ txt_val ref_ent)
  (if ent
    (progn
      (setq ed (entget ent))
      (setq typ (cdr (assoc 0 ed)))
      (cond
        ((or (= typ "TEXT") (= typ "MTEXT"))
         (setq txt_val (cdr (assoc 1 ed))))
        ((= typ "LEADER")
         ;; Neu la LEADER, tim doi tuong MTEXT kem theo qua ma DXF 340
         (setq ref_ent (cdr (assoc 340 ed)))
         (if ref_ent
           (setq txt_val (cdr (assoc 1 (entget ref_ent))))
           (setq txt_val nil)
         ))
        (T
         (setq txt_val nil))
      )
    )
    (setq txt_val nil)
  )
  txt_val
)

;; Ham ghi 1 dong vao file CSV an toan
(defun append_to_csv (csv_path row_str / f)
  (setq f (open csv_path "a"))
  (if f
    (progn
      (write-line row_str f)
      (close f)
      T
    )
    nil
  )
)

;; Ham lay so dong hien tai trong CSV de danh STT tiep theo
(defun get_current_stt (csv_path / f line count)
  (setq count 0)
  (if (findfile csv_path)
    (progn
      (setq f (open csv_path "r"))
      (if f
        (progn
          (while (read-line f)
            (setq count (1+ count))
          )
          (close f)
        )
      )
    )
  )
  (if (= count 0)
    1
    count ;; Neu da co Header (dong 1), dong tiep theo co STT = count
  )
)

;; Ham khoi tao Header cho file CSV neu file chua co
(defun init_csv_file (csv_path / f)
  (if (not (findfile csv_path))
    (progn
      (setq f (open csv_path "w"))
      (if f
        (progn
          (write-line "STT,LyTrinh,X1,Y1,X2,Y2,ChieuDai,GocXoay" f)
          (close f)
        )
      )
    )
  )
)

;; ==========================================================================
;; LENH CHINH: XTC (hoac XUAT_TIM_CONG) - Pick 2 diem dau cong (Khuyen dung)
;; ==========================================================================
(defun c:XTC (/ old_error old_cmdecho old_osmode csv_path stt cont p1 p2 x1 y1 x2 y2 len_c ang ent ent_name txt_val row_str)
  (setq old_error *error*)
  (setq *error* xtc_err)
  (setq old_cmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  (setq csv_path (get_safe_csv_path))
  (init_csv_file csv_path)
  (setq stt (get_current_stt csv_path))

  (princ "\n==========================================================================")
  (princ "\n>>> BAT DAU TRICH XUAT TIM CONG SANG CSV (DV_TOOL_HTKT) <<<")
  (princ (strcat "\n>>> File CSV dang luu tai: " csv_path))
  (princ "\n>>> HUONG DAN: ")
  (princ "\n    - Buoc 1: Pick diem dau 1 (Thuong luu / Dau vao)")
  (princ "\n    - Buoc 2: Pick diem dau 2 (Ha luu / Dau ra)")
  (princ "\n    - Buoc 3: Click vao TEXT / MTEXT / Mui ten ly trinh (hoac ENTER go tay)")
  (princ "\n    - An ESC de ket thuc khi xong toan bo tuyen.")
  (princ "\n==========================================================================")

  (setq cont T)
  (while cont
    (princ (strcat "\n\n--- [CONG SO " (itoa stt) "] ---"))
    (setq p1 (getpoint "\n1. Pick diem DAU CONG 1 (Thuong luu / Dau vao P1): "))
    (if (null p1)
      (setq cont nil)
      (progn
        ;; Day duong chun rubberband tu P1 den vi tri chuot
        (setq p2 (getpoint p1 "\n2. Pick diem DAU CONG 2 (Ha luu / Dau ra P2): "))
        (if (null p2)
          (setq cont nil)
          (progn
            (setq x1 (car p1))
            (setq y1 (cadr p1))
            (setq x2 (car p2))
            (setq y2 (cadr p2))
            (setq len_c (distance (list x1 y1) (list x2 y2)))
            (setq ang (r2d (angle (list x1 y1) (list x2 y2))))

            ;; Buoc 3: Chon text ly trinh
            (princ "\n3. Click vao TEXT Ly trinh tren ban ve (hoac an ENTER de go tay): ")
            (setq ent (entsel))
            (if ent
              (progn
                (setq txt_raw (get_text_from_entity (car ent)))
                (if (or (null txt_raw) (= txt_raw ""))
                  (setq txt_val (strcat "Cong_" (itoa stt)))
                  (setq txt_val (clean_text_value txt_raw))
                )
              )
              (progn
                (setq txt_input (getstring T (strcat "\nNhap ly trinh [Mac dinh Cong_" (itoa stt) "]: ")))
                (if (or (null txt_input) (= txt_input ""))
                  (setq txt_val (strcat "Cong_" (itoa stt)))
                  (setq txt_val (clean_text_value txt_input))
                )
              )
            )

            ;; Tao chuoi du lieu CSV
            (setq row_str (strcat (itoa stt) ","
                                  txt_val ","
                                  (rtos x1 2 4) ","
                                  (rtos y1 2 4) ","
                                  (rtos x2 2 4) ","
                                  (rtos y2 2 4) ","
                                  (rtos len_c 2 2) ","
                                  (rtos ang 2 2)))
            
            ;; Ghi ra file
            (if (append_to_csv csv_path row_str)
              (princ (strcat "\n==> [OK DA LUU]: " txt_val " | Dai L=" (rtos len_c 2 2) "m | Goc=" (rtos ang 2 1) " do"))
              (princ "\n==> [CANH BAO]: Khong the ghi vao file CSV! Kiem tra file co dang mo trong Excel khong!")
            )

            (setq stt (1+ stt))
          )
        )
      )
    )
  )

  (if old_cmdecho (setvar "CMDECHO" old_cmdecho))
  (setq *error* old_error)
  (princ (strcat "\n\n>>> HOAN TAT! Du lieu da duoc luu tai file: " csv_path " <<<"))
  (princ)
)

;; ==========================================================================
;; LENH PHU: XTCL - Click chon duong LINE/POLYLINE tim cong co san
;; Co bo sung tu dong kiem tra tranh loi khi click vao BLOCK hoac HATCH
;; ==========================================================================
(defun c:XTCL (/ old_error old_cmdecho csv_path stt ent ed typ obj p1 p2 x1 y1 x2 y2 len_c ang ent_txt txt_raw txt_val row_str)
  (setq old_error *error*)
  (setq *error* xtc_err)
  (setq old_cmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  (setq csv_path (get_safe_csv_path))
  (init_csv_file csv_path)
  (setq stt (get_current_stt csv_path))

  (princ "\nChon doan LINE hoac POLYLINE la tim cong tren Binh do: ")
  (setq ent (entsel))
  (if ent
    (progn
      (setq ed (entget (car ent)))
      (setq typ (cdr (assoc 0 ed)))
      (cond
        ((or (= typ "LINE") (= typ "LWPOLYLINE") (= typ "POLYLINE"))
         (setq obj (vlax-ename->vla-object (car ent)))
         (setq p1 (vlax-curve-getStartPoint obj))
         (setq p2 (vlax-curve-getEndPoint obj))
         (setq x1 (car p1))
         (setq y1 (cadr p1))
         (setq x2 (car p2))
         (setq y2 (cadr p2))
         (setq len_c (vlax-curve-getDistAtParam obj (vlax-curve-getEndParam obj)))
         (setq ang (r2d (angle (list x1 y1) (list x2 y2))))

         (princ "\nChon TEXT Ly trinh (hoac an ENTER de nhap tay): ")
         (setq ent_txt (entsel))
         (if ent_txt
           (setq txt_raw (get_text_from_entity (car ent_txt)))
           (setq txt_raw nil)
         )
         (if (or (null txt_raw) (= txt_raw ""))
           (progn
             (setq txt_val (getstring T (strcat "\nNhap ly trinh [Cong_" (itoa stt) "]: ")))
             (if (= txt_val "") (setq txt_val (strcat "Cong_" (itoa stt))))
           )
           (setq txt_val (clean_text_value txt_raw))
         )

         (setq row_str (strcat (itoa stt) "," txt_val "," (rtos x1 2 4) "," (rtos y1 2 4) "," (rtos x2 2 4) "," (rtos y2 2 4) "," (rtos len_c 2 2) "," (rtos ang 2 2)))
         (append_to_csv csv_path row_str)
         (princ (strcat "\n==> [OK DA LUU]: " txt_val " | L=" (rtos len_c 2 2) "m"))
        )
        
        ((= typ "INSERT")
         (princ "\n[CHU Y]: Doi tuong ban vua chon la BLOCK (Khong phai duong Line don).")
         (princ "\n[GOI Y]: Vui long dung lenh 'XTC' de pick truc tiep 2 diem dau cong (Thuong luu & Ha luu)!")
        )
        
        (T
         (princ (strcat "\n[CHU Y]: Doi tuong ban chon la " typ " khong phai duong Line/Polyline!"))
        )
      )
    )
  )

  (if old_cmdecho (setvar "CMDECHO" old_cmdecho))
  (setq *error* old_error)
  (princ)
)

;; ==========================================================================
;; CAC LENH ALIAS (Goi ten nao cung chay duoc)
;; ==========================================================================
(defun c:XUAT_TIM_CONG () (c:XTC))
(defun c:XUATTIMCONG () (c:XTC))
(defun c:TIMCONG () (c:XTC))
(defun c:LAYTOADO () (c:XTC))

;; Thong bao ro rang khi load LISP
(princ "\n==========================================================================")
(princ "\n  DA LOAD THANH CONG LISP XUAT TIM CONG V2.0 CHO DV_TOOL_HTKT!")
  (princ "\n  --> Go lenh 'XTC' hoac 'XUAT_TIM_CONG' de pick 2 diem dau cong (Khuyen dung)")
  (princ "\n  --> Go lenh 'XTCL' de click chon duong Line/Polyline tim cong co san")
(princ "\n==========================================================================")
(princ)
