# from utils import *
# from circle import *
"""
file = "2.txt"
time, voltage = preprocess(file)
radius, x, y = circle(time, voltage)

plt.plot(time, voltage)
plt.gca().add_patch(plt.Circle((x, y), radius, color='lightblue', alpha=0.5))
plt.show()
"""
import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
import openpyxl
import os
from scipy.ndimage import gaussian_filter
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
from math import sqrt
from itertools import islice
from openpyxl.utils import get_column_letter
import sys
import shutil
import warnings
import math


def open_txt(file):
    """
    Открывает текстовый файл и возвращает массивы времени и напряжения, разделенных табуляцией.

    Аргументы:
        file (str): Путь к текстовому файлу.

    Возвращает:
        tuple: Кортеж из двух массивов numpy (первый столбец данных, второй столбец данных).
    """
    with open(file, 'r') as file:
        file_content = file.read()
    file_content = file_content.replace(',', '.')

    with StringIO(file_content) as file_buffer:
        data = np.genfromtxt(file_buffer, delimiter='\t', usecols=(0, 1))

    return data[:, 0], data[:, 1]


def preprocess(file, smooth=15):
    """
    Предварительная обработка данных из файла.

    Аргументы:
        file (str): Путь к текстовому файлу с исходными данными.
        smooth (int, optional): Параметр сглаживания для алгоритма denoise_tv_chambolle.
            По умолчанию равен 15.

    Возвращает:
        tuple: Кортеж из двух массивов numpy (время и напряжение).
    """
    time, voltage = open_txt(file)

    voltage = denoise_tv_chambolle((voltage), smooth)
    voltage = gaussian_filter(voltage, 1)

    quality = 3

    return np.round(time, quality), np.round(voltage, quality)


def circle(time, voltage):
    def nearest_value(items_x, items_y, value_x, value_y):
        dist = np.sqrt((items_x - value_x) ** 2 + (items_y - value_y) ** 2)
        return np.argmin(dist)

    def flat(x, y, n):
        return x[::n], y[::n]

    def radius(x_c, y_c, x_1, y_1, x_2, y_2):
        cent1_x, cent1_y = (x_c + x_1) / 2, (y_c + y_1) / 2
        cent2_x, cent2_y = (x_c + x_2) / 2, (y_c + y_2) / 2

        k1 = (cent1_x - x_c) / (cent1_y - y_c)
        b1 = cent1_y + k1 * cent1_x

        k2 = (cent2_x - x_c) / (cent2_y - y_c)
        b2 = cent2_y + k2 * cent2_x

        x_r = (b2 - b1) / (k2 - k1)
        y_r = -k1 * x_r + b1

        rad = np.sqrt((x_r - x_c) ** 2 + (y_r - y_c) ** 2)
        return rad, x_r, y_r

    x = np.array(time)
    y = np.array(voltage)

    l = 8
    dff = flat(x, y, l)
    ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    o = 10

    # очень по гейски но что имеем
    while ma + o >= len(dff[0]):
        l = l // 2
        if l == 0:  # проверка на ноль и замена нуля на 1
            return 10, -10, 0

        dff = flat(x, y, l)
        ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    while dff[1][ma + o] - dff[1][ma] >= 8:
        if (dff[1][ma + o] - dff[1][ma]) / (dff[1][ma + (o - 1)] - dff[1][ma]) > 3.5:
            o -= 1
            ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))
            break
        o -= 1
        ma = nearest_value(dff[0], dff[1], x[np.argmax(y)], np.min(y))

    rad, x_r, y_r = radius(dff[0][ma], dff[1][ma], dff[0][ma + o], dff[1][ma + o], dff[0][ma - o], dff[1][ma - o])
    return rad, x_r, y_r



def find_action_potentials(time, voltage, alpha_threshold=25, refractory_period=10, start_offset=80):
    """
    Находит ПД в данных времени и напряжения.

    Функция анализирует производную напряжения по времени, выявляет начало фазы 0,
    пик и конец фазы 4 для каждого потенциала действия. Возвращает список словарей с
    индексами ключевых точек для каждого ПД.

    Аргументы:
        time (np.ndarray): 1D массив numpy содержащий значения времени.
        voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.
        alpha_threshold (float, optional): Пороговое значение производной напряжения
            для определения начала фазы 0. По умолчанию равен 25.
        refractory_period (int, optional): Рефрактерный период между значениями напряжения.
            По умолчанию равен 10.
        start_offset (int, optional): Смещение для определения начала фазы 0.
            По умолчанию равно 80.

    Возвращает:
        list: Список словарей с индексами ключевых точек для каждого ПД.
    """
    voltage_derivative = np.diff(voltage) / np.diff(time)

    candidate_phase_0_start_indices = np.where(voltage_derivative > alpha_threshold)[0]
    phase_0_start_indices = []
    for i in range(len(candidate_phase_0_start_indices)):
        if i == 0 or candidate_phase_0_start_indices[i] - candidate_phase_0_start_indices[i - 1] > refractory_period:
            phase_0_start_indices.append(candidate_phase_0_start_indices[i])

    action_potentials = []
    for i, start_index in enumerate(phase_0_start_indices):
        pre_start_index = action_potentials[-1]['end'] if i > 0 else 0

        if i < len(phase_0_start_indices) - 1:
            next_start_index = phase_0_start_indices[i + 1]
            peak_index = np.argmax(voltage[start_index:next_start_index]) + start_index
            end_index = np.argmin(voltage[peak_index:next_start_index]) + peak_index
        else:
            peak_index = np.argmax(voltage[start_index:]) + start_index
            end_index = np.argmin(voltage[peak_index:]) + peak_index

        action_potentials.append({
            'pre_start': pre_start_index,
            'start': start_index - start_offset,
            'peak': peak_index,
            'end': end_index
        })

    return action_potentials


def find_voltage_speed(ap, time, voltage):
    """
    Находит среднюю скорость изменения напряжения для фаз 4 и 0 ПД.

    Аргументы:
    ap (dict): Словарь с индексами ключевых точек ПД.
    time (np.ndarray): 1D массив numpy содержащий значения времени.
    voltage (np.ndarray): 1D массив numpy содержащий значения напряжения.

    Возвращает:
    tuple: Кортеж из двух чисел - средней скорости изменения напряжения для фазы 4 и фазы 0.
    """
    prestart_index = ap['pre_start']
    start_index = ap['start']
    peak_index = ap['peak']

    phase_4_time = time[prestart_index:start_index]
    phase_4_voltage = voltage[prestart_index:start_index]
    phase_4_speed = np.diff(phase_4_voltage) / np.diff(phase_4_time)

    phase_0_time = time[start_index:peak_index]
    phase_0_voltage = voltage[start_index:peak_index]
    phase_0_speed = np.diff(phase_0_voltage) / np.diff(phase_0_time)

    return 1000 * np.mean(phase_4_speed), np.mean(phase_0_speed)


def replace_nan_with_nearest(value_list, index):
    left, right = index - 1, index + 1

    try:
        while left >= 0 or right < len(value_list):
            if left >= 0 and not math.isnan(value_list[left]):
                return value_list[left]
            elif right < len(value_list) and not math.isnan(value_list[right]):
                return value_list[right]
            left -= 1
            right += 1
    finally:
        return None


# ----------------------------------------------------------------------------------------------
if __name__ == "__main__":
    C_sharp_data = sys.argv[1]
    # C_sharp_data = "D:/programming/projects/py/potentials/source/separated_APs/1.txt"
    warnings.filterwarnings("ignore")
    lines = C_sharp_data.split('\n')

    file_path = lines[0]

    inp_alpha_threshold = float(lines[1].replace(',', '.'))
    inp_start_offset = int(lines[2])
    inp_refractory_period = int(lines[3])

    """
    inp_alpha_threshold = 2.5
    inp_start_offset = 25
    inp_refractory_period = 10
    """

    time, voltage = open_txt(file_path)
    # time, voltage = preprocess(file_path)

    radius, x, y = circle(time, voltage)

    # добавим скорости в 0 и в 4 фазу
    # phase_4_speed, phase_0_speed = 999, 999
    aps = find_action_potentials(time, voltage, alpha_threshold=inp_alpha_threshold, start_offset=inp_start_offset, refractory_period=inp_refractory_period)
    phase_4_speed, phase_0_speed = 999, 999
    for number, ap in enumerate(aps):
        phase_4_speed, phase_0_speed = find_voltage_speed(ap, time, voltage)
        if math.isnan(phase_4_speed):
            phase_4_speed = 0
        if math.isnan(phase_0_speed):
            phase_0_speed = 0
        phase_4_speed = round(phase_4_speed, 3)
        phase_0_speed = round(phase_0_speed, 3)

    print(round(radius, 3))
    print(round(x, 3))
    print(round(y, 3))
    print(phase_0_speed)
    print(phase_4_speed)

# ----------------------------------------------------------------------------------------------

# Входные данные + резка пд
"""
file = "source/1.txt"
time, voltage = preprocess(file)
action_potentials = find_action_potentials(time, voltage, alpha_threshold=2.5, start_offset=25)

# Графики
plt.figure()
for ap in action_potentials:
    pre_start_index = ap['pre_start']
    end_index = ap['end']
    ap_time = time[pre_start_index:end_index + 1]
    ap_voltage = voltage[pre_start_index:end_index + 1]

    plt.plot(ap_time, ap_voltage)

plt.xlabel("Time")
plt.ylabel("Voltage")
plt.title("Merged Action Potentials")
plt.show()

for number, ap in enumerate(action_potentials):
    plot_ap(ap, time, voltage, number + 1)

plot_phase_4_speeds(action_potentials, time, voltage)
plot_phase_0_speeds(action_potentials, time, voltage)

# !!!очень долго выполняется
plot_radiuses(action_potentials, time, voltage)

# Табличка
destination = f"tables/{file.replace('.txt', '').replace('source/', '')}.xlsx"
save_aps_to_xlsx(destination, action_potentials, time, voltage)
"""
