import atypic_cardio_analyzer as can
import os
import sys
import shutil
import warnings
import math
from concurrent.futures import ProcessPoolExecutor
import dask.dataframe as dd


# ----------------------------------------------------------------------------------------------

if __name__ == "__main__":
    C_sharp_data = sys.argv[1]
    # C_sharp_data = "D:/programming/projects/py/source/long_sample_data2019.txt"

    lines = C_sharp_data.split('\n')
    destination = lines[0]
    file_path = lines[1]

    inp_alpha_threshold = float(lines[2].replace(',', '.'))
    inp_start_offset = int(lines[3])
    inp_refractory_period = int(lines[4])
    limit_rad = float(lines[5].replace(',', '.'))
    """
    file_path = C_sharp_data
    inp_alpha_threshold = 0.9
    inp_start_offset = 0
    inp_refractory_period = 280
    limit_rad = 250
    destination = "D:/programming/projects/2019longfff.xlsx"
    """
    warnings.filterwarnings("ignore")

    phase_4_speed_list = []
    phase_0_speed_list = []
    num_of_APs = []
    radius_list = []

    # time, voltage = open_txt(file_path)
    time, voltage = can.preprocess(file_path, step=4)

    # Резка ПД
    action_potentials = can.find_action_potentials(time, voltage, alpha=inp_alpha_threshold,
                                               offset=inp_start_offset, refractory_period=inp_refractory_period)

    # Сохранение в табличку
    try:
        can.save_aps_to_xlsx(destination, action_potentials, time, voltage, limit_rad)
        print("true")
        print("0")

    except Exception as e:
        print("false")
        print(f"{type(e).__name__}: {str(e)}")

# ----------------------------------------------------------------------------------------------
