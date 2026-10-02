;;; ==========================================================================
;;; CHUONG TRINH AUTOLISP: XUAT TIM CONG SANG CSV (DV_TOOL_HTKT - PHIEN BAN V3.0)
;;; Tac gia: DV_TOOL_HTKT
;;; Muc dich: Trich xuat toa do VN2000 (X1, Y1, Z1, X2, Y2, Z2), Chieu dai,
;;;           Goc xoay, Do doc va du lieu 21 COT CHUAN cho Add-in Revit
;;; Dinh dang so: So thuc thap phan chuan khong dau phay hang nghin (vd: 580860.018, 1477321.694)
;;; Lenh su dung: 
;;;   - XTC  hoac XUAT_TIM_CONG : Pick 2 diem dau cong (Khuyen dung)
;;;   - XTCL                    : Click chon duong Line/Polyline tim cong co san
;;;   - XTC_OPEN                : Mo truc tiep file CSV bang Excel de kiem tra
;;;   - XTC_RESET               : Khoi tao lai file CSV moi (xoa du lieu cu)
;;; ==========================================================================

(vl-load-com)

;; Bien toan cuc quan ly file va cai dat mac dinh
(setq *xtc_file* nil)
(if (null *xtc_def_loaicong*) (setq *xtc_def_loaicong* "CONG_HOP"))
(if (null *xtc_def_socua*)    (setq *xtc_def_socua* 1))
(if (null *xtc_def_khaudo*)   (setq *xtc_def_khaudo* "1.5x1.5"))
(if (null *xtc_def_sohn*)     (setq *xtc_def_sohn* 2))
(if (null *xtc_def_bht1*)     (setq *xtc_def_bht1* 1.50))
(if (null *xtc_def_bht2*)     (setq *xtc_def_bht2* 1.50))
(if (null *xtc_def_lngam*)    (setq *xtc_def_lngam* 0.30))
(if (null *xtc_def_kheho*)    (setq *xtc_def_kheho* 0.05))

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

;; Ham lay thu muc hien tai cua ban ve
(defun get_csv_dir ()
  (setq dwg_dir (getvar "DWGPREFIX"))
  (if (or (= dwg_dir "") (null dwg_dir))
    (setq dwg_dir "C:\\")
  )
  dwg_dir
)

;; Ham lay duong dan CSV chuan 21 cot
(defun get_safe_csv_path (/ dir def_path f)
  (setq dir (get_csv_dir))
  (setq def_path (strcat dir "DU_LIEU_CONG_NAP_TOOL.csv"))
  (setq f (open def_path "a"))
  (if f
    (progn
      (close f)
      def_path
    )
    (progn
      (setq alt_path (strcat dir "DU_LIEU_CONG_NAP_TOOL_" (menucmd "M=$(edtime,$(getvar,date),YYMODD_HHMMSS)") ".csv"))
      (princ (strcat "\n[Luu y]: File dang bi mo trong Excel! Luu sang: " alt_path))
      alt_path
    )
  )
)

;; Khoi tao Header chuan 21 COT
(defun init_csv_file (csv_path / f)
  (if (not (findfile csv_path))
    (progn
      (setq f (open csv_path "w"))
      (if f
        (progn
          (write-line "STT,LyTrinh,LoaiCong,SoCua,KhauDo,X1,Y1,Z1,X2,Y2,Z2,ChieuDai,DoDoc,GocXoay,SoHopNoi,KC_HN1,KC_HN2,B_HT1,B_HT2,L_Ngam_San,Khe_Ho_HN" f)
          (close f)
        )
      )
    )
  )
)

;; Ham lay STT tiep theo
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
    count
  )
)

;; Ham lam sach va phan tich Text tren ban ve
(defun clean_text_value (raw_str / i len ch res km_pos sub_km)
  (if (or (null raw_str) (= raw_str ""))
    "Km 0+000"
    (progn
      (setq len (strlen raw_str))
      (setq res "")
      (setq i 1)
      (while (<= i len)
        (setq ch (substr raw_str i 1))
        (cond
          ((and (= ch "\\") (<= (1+ i) len) (= (strcase (substr raw_str (1+ i) 1)) "P"))
           (setq res (strcat res " "))
           (setq i (1+ i)))
          ((or (= ch ",") (= ch ";") (= ch "\"") (= ch "}") (= ch "{"))
           (setq res (strcat res " ")))
          (T
           (setq res (strcat res ch)))
        )
        (setq i (1+ i))
      )
      (setq res (vl-string-trim " " res))
      res
    )
  )
)

;; Ham doc Text tu doi tuong ent
(defun get_text_from_entity (ent / ed typ txt_val ref_ent)
  (if ent
    (progn
      (setq ed (entget ent))
      (setq typ (cdr (assoc 0 ed)))
      (cond
        ((or (= typ "TEXT") (= typ "MTEXT"))
         (setq txt_val (cdr (assoc 1 ed))))
        ((= typ "LEADER")
         (setq ref_ent (cdr (assoc 340 ed)))
         (if ref_ent
           (setq txt_val (cdr (assoc 1 (entget ref_ent))))
           (setq txt_val nil)
         ))
        (T (setq txt_val nil))
      )
    )
    (setq txt_val nil)
  )
  txt_val
)

;; Ham ghi dong vao CSV
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

;; Ham tu dong nhan dien loai cong tu chuoi text
(defun auto_detect_culvert_info (txt / up)
  (setq up (strcase txt))
  (cond
    ((or (vl-string-search "HOP" up) (vl-string-search "HỘP" up) (vl-string-search "CH" up) (vl-string-search "X" up))
     (setq *xtc_def_loaicong* "CONG_HOP"))
    ((or (vl-string-search "TRON" up) (vl-string-search "TRÒN" up) (vl-string-search "CT" up) (vl-string-search "D" up))
     (setq *xtc_def_loaicong* "CONG_TRON"))
  )
  (if (or (vl-string-search "2D" up) (vl-string-search "2X" up) (vl-string-search "ĐÔI" up) (vl-string-search "DOI" up))
    (setq *xtc_def_socua* 2)
  )
)

;; ==========================================================================
;; LENH CHINH: XTC (hoac XUAT_TIM_CONG) - Pick 2 diem dau cong (Chuan 21 cot)
;; ==========================================================================
(defun c:XTC (/ old_error old_cmdecho old_osmode csv_path stt cont
                p1 p2 x1 y1 z1 x2 y2 z2 len_c ang do_doc ent txt_raw ly_trinh
                loai_cong so_cua khau_do so_hn b_ht1 b_ht2 l_ngam khe_ho row_str)
  (setq old_error *error*)
  (setq *error* xtc_err)
  (setq old_cmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  (setq csv_path (get_safe_csv_path))
  (init_csv_file csv_path)
  (setq stt (get_current_stt csv_path))

  (princ "\n==========================================================================")
  (princ "\n>>> XUAT TIM CONG SANG CSV CHUAN 21 COT (DV_TOOL_HTKT V3.0) <<<")
  (princ (strcat "\n>>> File luu tai: " csv_path))
  (princ "\n>>> DINH DANG TOA DO: Khong dau phay hang nghin (Chuan 0.000 nap thang vao Revit)")
  (princ "\n>>> Thao tac:")
  (princ "\n    - Pick P1 (Thuong luu) -> Pick P2 (Ha luu)")
  (princ "\n    - Click TEXT ly trinh tren CAD (hoac Enter go tay)")
  (princ "\n    - An ESC khi hoan tat toan bo tuyen.")
  (princ "\n==========================================================================")

  (setq cont T)
  (while cont
    (princ (strcat "\n\n--- [CONG STT " (itoa stt) "] ---"))
    (setq p1 (getpoint "\n1. Pick diem DAU CONG 1 (Thuong luu P1): "))
    (if (null p1)
      (setq cont nil)
      (progn
        (setq p2 (getpoint p1 "\n2. Pick diem DAU CONG 2 (Ha luu P2): "))
        (if (null p2)
          (setq cont nil)
          (progn
            ;; Toa do x, y, z (P1 va P2)
            (setq x1 (car p1))
            (setq y1 (cadr p1))
            (setq z1 (if (caddr p1) (caddr p1) 0.0))

            (setq x2 (car p2))
            (setq y2 (cadr p2))
            (setq z2 (if (caddr p2) (caddr p2) 0.0))

            ;; Chieu dai 2D va goc xoay Azimuth
            (setq len_c (distance (list x1 y1) (list x2 y2)))
            (setq ang (r2d (angle (list x1 y1) (list x2 y2))))

            ;; Tinh do doc (%) neu z1, z2 co cao do thuc
            (if (and (> len_c 0.001) (/= z1 z2) (/= z1 0.0))
              (setq do_doc (abs (* (/ (- z1 z2) len_c) 100.0)))
              (setq do_doc 0.45)
            )

            ;; Buoc 3: Chon Text ly trinh
            (princ "\n3. Click vao TEXT Ly trinh tren ban ve (hoac ENTER de go tay): ")
            (setq ent (entsel))
            (if ent
              (progn
                (setq txt_raw (get_text_from_entity (car ent)))
                (if (or (null txt_raw) (= txt_raw ""))
                  (setq ly_trinh (strcat "Km " (itoa stt) "+000"))
                  (progn
                    (setq ly_trinh (clean_text_value txt_raw))
                    (auto_detect_culvert_info txt_raw)
                  )
                )
              )
              (progn
                (setq txt_input (getstring T (strcat "\nNhap ly trinh [Mac dinh Km " (itoa stt) "+000]: ")))
                (if (or (null txt_input) (= txt_input ""))
                  (setq ly_trinh (strcat "Km " (itoa stt) "+000"))
                  (setq ly_trinh (clean_text_value txt_input))
                )
              )
            )

            ;; Cac tham so mac dinh 21 cot
            (setq loai_cong *xtc_def_loaicong*)
            (setq so_cua *xtc_def_socua*)
            (setq khau_do *xtc_def_khaudo*)
            (setq so_hn *xtc_def_sohn*)
            (setq b_ht1 *xtc_def_bht1*)
            (setq b_ht2 *xtc_def_bht2*)
            (setq l_ngam *xtc_def_lngam*)
            (setq khe_ho *xtc_def_kheho*)

            ;; Khoang cach ho thu mac dinh (neu 2 ho thu thi dat doi xung)
            (setq kc_hn1 (if (> so_hn 0) 0.38 0.0))
            (setq kc_hn2 (if (> so_hn 1) 0.38 0.0))

            ;; Tao chuoi du lieu 21 COT CHUAN (rtos voi che do 2 la thap phan KHONG DAU PHAY HANG NGHIN)
            (setq row_str (strcat
              (itoa stt) ","
              ly_trinh ","
              loai_cong ","
              (itoa so_cua) ","
              khau_do ","
              (rtos x1 2 3) ","
              (rtos y1 2 3) ","
              (rtos z1 2 3) ","
              (rtos x2 2 3) ","
              (rtos y2 2 3) ","
              (rtos z2 2 3) ","
              (rtos len_c 2 2) ","
              (rtos do_doc 2 2) ","
              (rtos ang 2 2) ","
              (itoa so_hn) ","
              (rtos kc_hn1 2 2) ","
              (rtos kc_hn2 2 2) ","
              (rtos b_ht1 2 2) ","
              (rtos b_ht2 2 2) ","
              (rtos l_ngam 2 2) ","
              (rtos khe_ho 2 2)
            ))

            ;; Ghi ra file
            (if (append_to_csv csv_path row_str)
              (princ (strcat "\n==> [OK DA LUU 21 COT]: " ly_trinh
                             " | P1=(" (rtos x1 2 3) "; " (rtos y1 2 3) ")"
                             " -> P2=(" (rtos x2 2 3) "; " (rtos y2 2 3) ")"
                             " | L=" (rtos len_c 2 2) "m"))
              (princ "\n==> [CANH BAO]: Khong the ghi file CSV! Kiem tra file co dang mo khong!")
            )

            (setq stt (1+ stt))
          )
        )
      )
    )
  )

  (if old_cmdecho (setvar "CMDECHO" old_cmdecho))
  (setq *error* old_error)
  (princ (strcat "\n\n>>> HOAN TAT! Da luu 21 cot tai: " csv_path " <<<"))
  (princ "\n>>> Go lenh 'XTC_OPEN' de mo xem file CSV ngay trong Excel.")
  (princ)
)

;; ==========================================================================
;; LENH PHU: XTCL - Click chon duong LINE/POLYLINE tim cong co san
;; ==========================================================================
(defun c:XTCL (/ old_error old_cmdecho csv_path stt ent ed typ obj
                 p1 p2 x1 y1 z1 x2 y2 z2 len_c ang ent_txt txt_raw ly_trinh row_str)
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
         (setq z1 (if (caddr p1) (caddr p1) 0.0))
         (setq x2 (car p2))
         (setq y2 (cadr p2))
         (setq z2 (if (caddr p2) (caddr p2) 0.0))
         (setq len_c (vlax-curve-getDistAtParam obj (vlax-curve-getEndParam obj)))
         (setq ang (r2d (angle (list x1 y1) (list x2 y2))))

         (princ "\nChon TEXT Ly trinh tren ban ve (hoac ENTER de go tay): ")
         (setq ent_txt (entsel))
         (if ent_txt
           (setq txt_raw (get_text_from_entity (car ent_txt)))
           (setq txt_raw nil)
         )
         (if (or (null txt_raw) (= txt_raw ""))
           (progn
             (setq ly_trinh (getstring T (strcat "\nNhap ly trinh [Km " (itoa stt) "+000]: ")))
             (if (= ly_trinh "") (setq ly_trinh (strcat "Km " (itoa stt) "+000")))
           )
           (progn
             (setq ly_trinh (clean_text_value txt_raw))
             (auto_detect_culvert_info txt_raw)
           )
         )

         (setq row_str (strcat
           (itoa stt) ","
           ly_trinh ","
           *xtc_def_loaicong* ","
           (itoa *xtc_def_socua*) ","
           *xtc_def_khaudo* ","
           (rtos x1 2 3) ","
           (rtos y1 2 3) ","
           (rtos z1 2 3) ","
           (rtos x2 2 3) ","
           (rtos y2 2 3) ","
           (rtos z2 2 3) ","
           (rtos len_c 2 2) ","
           "0.45,"
           (rtos ang 2 2) ","
           (itoa *xtc_def_sohn*) ","
           "0.38,0.38,"
           (rtos *xtc_def_bht1* 2 2) ","
           (rtos *xtc_def_bht2* 2 2) ","
           (rtos *xtc_def_lngam* 2 2) ","
           (rtos *xtc_def_kheho* 2 2)
         ))

         (append_to_csv csv_path row_str)
         (princ (strcat "\n==> [OK DA LUU 21 COT]: " ly_trinh " | L=" (rtos len_c 2 2) "m"))
        )
        (T
         (princ (strcat "\n[CHU Y]: Doi tuong ban chon la " typ " khong phai Line/Polyline!"))
        )
      )
    )
  )

  (if old_cmdecho (setvar "CMDECHO" old_cmdecho))
  (setq *error* old_error)
  (princ)
)

;; ==========================================================================
;; LENH TIEN ICH: XTC_OPEN (Mo file CSV) va XTC_RESET (Xoa lam lai)
;; ==========================================================================
(defun c:XTC_OPEN (/ csv_path)
  (setq csv_path (strcat (get_csv_dir) "DU_LIEU_CONG_NAP_TOOL.csv"))
  (if (findfile csv_path)
    (progn
      (startapp "explorer.exe" csv_path)
      (princ (strcat "\nDa mo file: " csv_path))
    )
    (princ "\nChua co file CSV. Vui long dung lenh XTC de xuat truoc!")
  )
  (princ)
)

(defun c:XTC_RESET (/ csv_path ans)
  (setq csv_path (strcat (get_csv_dir) "DU_LIEU_CONG_NAP_TOOL.csv"))
  (if (findfile csv_path)
    (progn
      (initget "Y N")
      (setq ans (getkword "\nBan co chac chan muon XOA file CSV de bat dau lai tu STT 1? [Y/N] <N>: "))
      (if (= ans "Y")
        (progn
          (vl-file-delete csv_path)
          (init_csv_file csv_path)
          (princ "\nDa khoi tao lai file CSV moi tu STT 1!")
        )
        (princ "\nDa huy thao tac.")
      )
    )
    (progn
      (init_csv_file csv_path)
      (princ "\nDa tao moi file CSV 21 cot!")
    )
  )
  (princ)
)

;; Cac lenh Alias tien dung
(defun c:XUAT_TIM_CONG () (c:XTC))
(defun c:XUATTIMCONG () (c:XTC))
(defun c:TIMCONG () (c:XTC))
(defun c:LAYTOADO () (c:XTC))

(princ "\n==========================================================================")
(princ "\n  DA LOAD THANH CONG LISP XUAT TIM CONG V3.0 (21 COT CHUAN DV_TOOL_HTKT)!")
(princ "\n  --> Lenh 'XTC'       : Pick 2 diem tim cong xuat 21 cot (Khuyen dung)")
(princ "\n  --> Lenh 'XTCL'      : Click chon Polyline/Line co san")
(princ "\n  --> Lenh 'XTC_OPEN'  : Mo file CSV kiem tra ngay tren Excel")
(princ "\n  --> Lenh 'XTC_RESET' : Khoi tao lai file tu STT 1")
(princ "\n==========================================================================")
(princ)
